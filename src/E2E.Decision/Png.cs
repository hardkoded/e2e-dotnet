// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Decision;

/// <summary>Upstream's <c>png.ts</c>: PNG bytes to 8-bit RGBA and back. Skia does the decoding in the port, where upstream uses <c>@napi-rs/image</c>.</summary>
internal static class Png
{
    /// <summary>Upstream's <c>decodeRgba</c>. Decodes PNG bytes to 8-bit RGBA, whatever the color type, bit depth, or palette; throws on bytes that are not a PNG.</summary>
    public static RgbaImage DecodeRgba(byte[] bytes) => ImageOps.DecodePng(bytes);

    /// <summary>Upstream's <c>encodeRgba</c>. Encodes an image as PNG bytes.</summary>
    public static byte[] EncodeRgba(RgbaImage image) => ImageOps.EncodePng(image);
}
