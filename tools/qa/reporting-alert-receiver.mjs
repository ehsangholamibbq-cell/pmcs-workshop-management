import { createServer } from "node:http";

const configuredPort = process.env.PMCS_QA_ALERT_RECEIVER_PORT ?? "19093";
const port = Number.parseInt(configuredPort, 10);
if (!Number.isSafeInteger(port) || String(port) !== configuredPort || port < 1024 || port > 65535) {
  throw new Error("PMCS_QA_ALERT_RECEIVER_PORT must be an integer between 1024 and 65535.");
}

const events = [];
const maximumBodyBytes = 1024 * 1024;

const sendJson = (response, statusCode, payload) => {
  const body = JSON.stringify(payload);
  response.writeHead(statusCode, {
    "content-length": Buffer.byteLength(body),
    "content-type": "application/json; charset=utf-8",
  });
  response.end(body);
};

const server = createServer((request, response) => {
  const path = new URL(request.url ?? "/", `http://${request.headers.host ?? "127.0.0.1"}`).pathname;
  if (request.method === "GET" && path === "/health") {
    sendJson(response, 200, { status: "ready" });
    return;
  }
  if (request.method === "GET" && path === "/events") {
    sendJson(response, 200, { events });
    return;
  }
  if (request.method !== "POST" || path !== "/alerts") {
    sendJson(response, 404, { error: "not_found" });
    return;
  }

  const chunks = [];
  let size = 0;
  request.on("data", (chunk) => {
    size += chunk.length;
    if (size > maximumBodyBytes) {
      request.destroy(new Error("Alert payload exceeded the QA receiver limit."));
      return;
    }
    chunks.push(chunk);
  });
  request.on("end", () => {
    try {
      const payload = JSON.parse(Buffer.concat(chunks).toString("utf8"));
      events.push({ receivedAt: new Date().toISOString(), payload });
      if (events.length > 100) events.shift();
      response.writeHead(204);
      response.end();
    } catch {
      sendJson(response, 400, { error: "invalid_json" });
    }
  });
});

server.on("clientError", (_error, socket) => socket.end("HTTP/1.1 400 Bad Request\r\n\r\n"));
server.listen(port, "127.0.0.1", () => {
  process.stdout.write(`${JSON.stringify({ status: "ready", port })}\n`);
});

const stop = () => server.close(() => process.exit(0));
process.on("SIGINT", stop);
process.on("SIGTERM", stop);

