import assert from "node:assert/strict";
import test from "node:test";
import { plainText } from "./text.mjs";

test("plainText keeps underscores in identifiers", () => {
    assert.equal(plainText("Reads `AWS_BEARER_TOKEN_BEDROCK` from the environment."), "Reads AWS_BEARER_TOKEN_BEDROCK from the environment.");
});

test("plainText keeps angle brackets escaped", () => {
    assert.equal(plainText("Returns a Task&lt;T&gt; value."), "Returns a Task&lt;T&gt; value.");
});
