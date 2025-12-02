import { NextRequest, NextResponse } from "next/server";

export async function POST(req: NextRequest) {
  try {
    console.log("Received request for image description");

    // Parse body explicitly to return a precise error for malformed JSON
    let body: unknown;
    try {
      body = (await req.json()) as unknown;
    } catch (e) {
      const message = e instanceof Error ? e.message : String(e);
      return NextResponse.json(
        { error: "Invalid JSON body", details: message },
        { status: 400 }
      );
    }

    if (!body || typeof body !== "object") {
      return NextResponse.json(
        { error: "Invalid JSON body", details: "Expected a JSON object" },
        { status: 400 }
      );
    }

    const imageUrlVal = (body as Record<string, unknown>)["imageUrl"];
    if (typeof imageUrlVal !== "string" || imageUrlVal.trim() === "") {
      return NextResponse.json(
        {
          error: "imageUrl is required",
          details: "Provide a non-empty string",
        },
        { status: 400 }
      );
    }
    const imageUrl = imageUrlVal;

    // Validate imageUrl format and protocol
    try {
      const u = new URL(imageUrl);
      if (u.protocol !== "http:" && u.protocol !== "https:") {
        return NextResponse.json(
          {
            error: "Invalid imageUrl protocol",
            details: `Expected http(s), got ${u.protocol || "unknown"}`,
          },
          { status: 400 }
        );
      }
    } catch {
      return NextResponse.json(
        { error: "Invalid imageUrl", details: "Value is not a valid URL" },
        { status: 400 }
      );
    }

    const apiKey = process.env.GROQ_API_KEY;
    if (!apiKey) {
      return NextResponse.json(
        { error: "GROQ_API_KEY is not configured" },
        { status: 500 }
      );
    }

    // Use a vision-capable model and request ONLY the error message text
    const model = "meta-llama/llama-4-maverick-17b-128e-instruct";

    const response = await fetch(
      "https://api.groq.com/openai/v1/chat/completions",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${apiKey}`,
        },
        body: JSON.stringify({
          model,
          messages: [
                    {
                      role: "system",
                      content: `
                  You are a log extractor that reads screenshots of console/output windows.

                  When the user sends you a screenshot:

                  1. Read ALL text visible in the image.
                  2. Identify all lines that look like errors or failures (e.g. containing "fail:", "error", "exception").
                  3. Also include any nearby "info" lines that change the meaning of the error (for example: migrations being applied, application started, listening on a port, etc.).
                  4. Return a concise response with exactly these three sections:

                  [Full Relevant Log]
                  <verbatim copy of all error/fail lines and their surrounding context>

                  [Plain Description]
                  One or two short sentences describing what happened in neutral language.

                  Rules:
                  - Do NOT give any suggestions, analysis, diagnosis, root causes, or fixes.
                  - Do NOT add any additional commentary outside the three sections.
                  - Do NOT invent log lines that are not visible in the screenshot.
                  `.trim(),
                    },
                    {
                      role: "user",
                      content: [
                        {
                          type: "text",
                          text: "Extract the full error block and key context from this screenshot and restate what happened.",
                        },
                        {
                          type: "image_url",
                          image_url: {
                            url: imageUrl, // e.g. "/mnt/data/migration2025-11-22 162034.jpg"
                          },
                        },
                      ],
                    },
                  ],
          temperature: 0,
          max_tokens: 256,
        }),
      }
    );

    if (!response.ok) {
      const requestId =
        response.headers.get("x-request-id") ||
        response.headers.get("x-groq-request-id") ||
        undefined;

      const statusText = response.statusText;
      let details: unknown;
      let raw: string | undefined;

      try {
        const text = await response.text();
        raw = text;
        try {
          const json = JSON.parse(text);
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          details = (json && ((json as any).error ?? json)) || text;
        } catch {
          details = text;
        }
      } catch (err) {
        details = String(err);
      }

      console.error("Groq error:", response.status, statusText, details);
      return NextResponse.json(
        {
          error: "Groq request failed",
          status: response.status,
          statusText,
          requestId,
          details,
          ...(raw ? { raw: raw.slice(0, 2000) } : {}),
        },
        { status: 502 }
      );
    }

    const data = await response.json();
    const raw = data.choices?.[0]?.message?.content ?? "";
    const description =
      String(raw)
        // strip common labels if the model still returned any
        .replace(
          /^(\*\*?)?\s*(Root Cause|Fix|Solution|How to fix)[:：].*$/gim,
          ""
        )
        .replace(/\*\*/g, "")
        .trim() || "No description generated.";

    return NextResponse.json({ description });
  } catch (err) {
    const details = err instanceof Error ? err.message : String(err);
    console.error(err);
    return NextResponse.json(
      {
        error: "Unexpected error generating image description",
        details,
      },
      { status: 500 }
    );
  }
}
