"""Placeholder AI Services container (AIS-01..07).

Track 2 owns this container's real implementation (self-hosted models for
AIS-01/03/04/07, Copyleaks/Pangram integration for AIS-02/05). This stub only
exists so `docker compose up` brings up every container Section 11 of the
architecture doc describes, with a /health endpoint for the other containers
to depend_on.
"""

from http.server import BaseHTTPRequestHandler, HTTPServer


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/health":
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b'{"status":"stub"}')
        else:
            self.send_response(404)
            self.end_headers()


if __name__ == "__main__":
    HTTPServer(("0.0.0.0", 8000), Handler).serve_forever()
