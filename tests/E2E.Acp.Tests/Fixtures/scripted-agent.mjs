// A scripted ACP agent on stdio, for tests. The port of upstream's fixtures/scripted-agent.ts without the SDKs: it speaks the
// protocol's JSON lines and the step tools' MCP over HTTP itself.
//
// `ACP_SCRIPT` is a JSON array with one entry per prompt turn, or an object keyed by text the prompt contains, each a list of moves:
// call one of the client's MCP tools or several at once, ask permission for a tool of the agent's own, run one without asking, say
// something, fail the turn with a message in the error's data, or hang until cancelled or for good. Every prompt and tool result is
// appended to `ACP_LOG` as JSON lines, with the tool list the session saw, and the `E2E_` variables and `CODEX_CONFIG` it started
// with. `ACP_USAGE` is the usage each turn reports. With `ACP_HANG_INIT` set, the agent never answers `initialize`.

import { appendFileSync } from 'node:fs';
import { createInterface } from 'node:readline';

const script = JSON.parse(process.env.ACP_SCRIPT ?? '[]');
const movesFor = (prompt, index) =>
  Array.isArray(script) ? (script[index] ?? []) : (Object.entries(script).find(([key]) => prompt.includes(key))?.[1] ?? []);
const log = (entry) => {
  if (process.env.ACP_LOG !== undefined) appendFileSync(process.env.ACP_LOG, `${JSON.stringify(entry)}\n`);
};

log({ pid: process.pid });
log({
  env: Object.keys(process.env).filter((name) => name.toUpperCase().startsWith('E2E_')).sort(),
  codexConfig: process.env.CODEX_CONFIG ?? null,
});

let mcpUrl;
let mcpId = 0;
let turn = 0;
let cancelRequested = false;
let cancelled;
let nextId = 1000;
const waiting = new Map();

const send = (message) => process.stdout.write(`${JSON.stringify({ jsonrpc: '2.0', ...message })}\n`);
const ask = (method, params) =>
  new Promise((resolve, reject) => {
    const id = nextId++;
    waiting.set(id, { resolve, reject });
    send({ id, method, params });
  });
const notify = (method, params) => send({ method, params });
const update = (value) => notify('session/update', { sessionId: 's1', update: value });

async function mcp(method, params) {
  const response = await fetch(mcpUrl, {
    method: 'POST',
    headers: { 'content-type': 'application/json', accept: 'application/json, text/event-stream' },
    body: JSON.stringify({ jsonrpc: '2.0', id: ++mcpId, method, params }),
  });
  const body = await response.json();
  if (body.error) throw new Error(body.error.message);
  return body.result;
}
const callTool = (name, args) => mcp('tools/call', { name, arguments: { ...args } });

const handlers = {
  initialize: () =>
    process.env.ACP_HANG_INIT === undefined
      ? {
          protocolVersion: 1,
          agentCapabilities: { mcpCapabilities: { http: true }, sessionCapabilities: { close: {} } },
          agentInfo: { name: 'scripted-agent', version: '1' },
          authMethods: [],
        }
      : new Promise(() => undefined),
  'session/new': async (params) => {
    const server = params.mcpServers[0];
    mcpUrl = server.url;
    await mcp('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'scripted-agent', version: '1' } });
    const { tools } = await mcp('tools/list', {});
    log({ session: { cwd: params.cwd, server: server.name, meta: params._meta ?? null, tools: tools.map((tool) => tool.name) } });
    return {
      sessionId: 's1',
      configOptions: [
        {
          id: 'model',
          name: 'Model',
          category: 'model',
          type: 'select',
          currentValue: 'fast',
          options: [
            { value: 'fast', name: 'Fast' },
            { value: 'slow', name: 'Slow' },
          ],
        },
      ],
      modes: {
        currentModeId: 'default',
        availableModes: [
          { id: 'default', name: 'Default' },
          { id: 'read-only', name: 'Read only' },
        ],
      },
    };
  },
  'session/set_mode': (params) => {
    log({ mode: params.modeId });
    return {};
  },
  'session/set_config_option': (params) => {
    log({ config: { [params.configId]: params.value } });
    return { configOptions: [] };
  },
  authenticate: () => ({}),
  'session/close': () => {
    log({ closed: true });
    return {};
  },
  'session/cancel': () => {
    log({ cancelled: true });
    cancelRequested = true;
    cancelled?.();
  },
  'session/prompt': async (params) => {
    const text = params.prompt.map((block) => (block.type === 'text' ? block.text : '')).join('\n');
    log({ prompt: text });
    cancelRequested = false;
    for (const move of movesFor(text, turn++)) {
      if ('say' in move) {
        update({ sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: move.say } });
      } else if ('fail' in move) {
        throw { code: -32603, message: 'Internal error', data: { message: move.fail } };
      } else if ('own' in move) {
        const toolCall = {
          toolCallId: `own-${turn}-${move.own}`,
          title: move.own,
          ...(move.kind === undefined ? {} : { kind: move.kind }),
          ...(move.rawInput === undefined ? {} : { rawInput: move.rawInput }),
          ...(move.meta === undefined ? {} : { _meta: move.meta }),
        };
        update({ sessionUpdate: 'tool_call', ...toolCall });
        const answer = await ask('session/request_permission', {
          sessionId: 's1',
          toolCall,
          ...(move.requestMeta === undefined ? {} : { _meta: move.requestMeta }),
          options: [
            { optionId: 'yes', name: 'Allow', kind: 'allow_once' },
            { optionId: 'no', name: 'Reject', kind: 'reject_once' },
          ],
        });
        log({ permission: move.own, outcome: answer.outcome });
        if (move.status !== undefined) update({ sessionUpdate: 'tool_call_update', toolCallId: toolCall.toolCallId, status: move.status });
      } else if ('ran' in move) {
        // A tool of the agent's own that runs without asking.
        const toolCallId = `ran-${turn}-${move.ran}`;
        update({ sessionUpdate: 'tool_call', toolCallId, title: move.ran, kind: move.kind ?? 'read', status: 'in_progress' });
        if (move.status !== 'in_progress') update({ sessionUpdate: 'tool_call_update', toolCallId, status: move.status ?? 'completed' });
        log({ ran: move.ran });
        // The notification and the next tool call travel on different channels: let the client read the first before the second.
        await new Promise((resolve) => setTimeout(resolve, 100));
      } else if ('batch' in move) {
        // Sent in order, a few milliseconds apart, so they reach the server in order; none waits for the one before it.
        const sent = [];
        for (const { call, args } of move.batch) {
          sent.push(callTool(call, args).then((result) => log({ call, result })));
          await new Promise((resolve) => setTimeout(resolve, 20));
        }
        await Promise.all(sent);
      } else if ('ignoreCancel' in move) {
        await new Promise(() => undefined);
      } else if ('hang' in move) {
        if (!cancelRequested) await new Promise((resolve) => (cancelled = resolve));
        if (move.thenRan !== undefined) {
          // Logged first: the executor stops this agent as soon as it reports the tool.
          log({ ran: move.thenRan });
          update({ sessionUpdate: 'tool_call', toolCallId: `ran-${turn}-${move.thenRan}`, title: move.thenRan, kind: 'read', status: 'in_progress' });
        }
        return { stopReason: 'cancelled' };
      } else {
        log({ call: move.call, result: await callTool(move.call, move.args) });
      }
    }
    update({ sessionUpdate: 'usage_update', used: 100, size: 1000, cost: { amount: 0.01 * turn, currency: 'USD' } });
    return {
      stopReason: 'end_turn',
      usage: JSON.parse(process.env.ACP_USAGE ?? '{"totalTokens":15,"inputTokens":10,"outputTokens":5,"cachedReadTokens":4}'),
    };
  },
};

createInterface({ input: process.stdin }).on('line', (line) => {
  if (line.trim() === '') return;
  const message = JSON.parse(line);
  if (message.method !== undefined) {
    const handler = handlers[message.method];
    if (message.id === undefined) {
      void handler?.(message.params);
      return;
    }
    Promise.resolve()
      .then(() => (handler === undefined ? Promise.reject({ code: -32601, message: `Method not found: ${message.method}` }) : handler(message.params)))
      .then(
        (result) => send({ id: message.id, result: result ?? null }),
        (error) => send({ id: message.id, error: { code: error.code ?? -32603, message: error.message ?? String(error), data: error.data } }),
      );
  } else if (waiting.has(message.id)) {
    const { resolve, reject } = waiting.get(message.id);
    waiting.delete(message.id);
    if (message.error) reject(message.error);
    else resolve(message.result);
  }
});
