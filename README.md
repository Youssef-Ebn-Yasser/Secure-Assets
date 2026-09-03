# Secure Media Vault

A microservice-based secure media storage and streaming platform that processes video, image, and PDF uploads into **non-downloadable token-gated chunks** and delivers them to an Angular frontend for **memory-only reassembly**.

---

## 🔒 Security Architecture

- **Private Storage**: MinIO object storage runs on a private internal Docker network with **no exposed public port**. All media is strictly accessed via token-gated proxy routes.
- **Dynamic HLS AES-128 Video**: Transcodes videos into 4-second `.ts` segments encrypted with dynamic keys; playlists (`.m3u8`) are constructed dynamically on the fly with short-lived HMAC signed segment URLs (~30s expiry).
- **In-Memory Image Tiling**: Strips EXIF metadata, splits images into an $N \times M$ WebP tile grid, and stitches them directly onto an HTML5 `<canvas>`. No complete image exists in browser disk/network cache.
- **PDF Page Slicing**: Converts multi-page documents into individual WebP page slices stored under randomized GUID server directories and streamed sequentially.
- **Anti-Replay & Rate Limiting**: HMAC-SHA256 tokens tracked in Redis / Nginx rate limiting zones to prevent scraping.
- **Security UX Layer**: Right-click context menus disabled, shortcut interception (`Ctrl+S`, `Ctrl+P`), dynamic user watermarking.

---

## 🧩 Microservices

| Service | Technology | Role |
|---|---|---|
| `gateway` | Nginx Alpine | Reverse proxy, TLS, rate limiting, security headers |
| `frontend` | Angular 18 (Green Theme) | Memory-only canvas & HLS media players, upload zone |
| `api` | ASP.NET Core 8 Web API | JWT Auth, upload ingress, HMAC token issuer, chunk proxy |
| `ffmpeg-worker` | .NET 8 Worker + FFmpeg | Consumes video jobs, generates AES-128 HLS chunks |
| `image-worker` | .NET 8 Worker + ImageSharp | Consumes image jobs, generates $N \times M$ WebP tiles |
| `pdf-worker` | .NET 8 Worker + PDFtoImage | Consumes PDF jobs, extracts page slices to GUID folders |
| `postgres` | PostgreSQL 16 | User accounts, file metadata, chunk manifests |
| `redis` | Redis 7 | Token replay counter, rate limiting |
| `rabbitmq` | RabbitMQ 3 Management | Asynchronous message queues |
| `minio` | MinIO Object Storage | Internal encrypted blob storage |

---

## 🚀 Quick Start (Docker Compose)

1. **Configure Environment:**
   ```bash
   cp .env.example .env
   ```

2. **Start All Services:**
   ```bash
   docker compose up -d --build
   ```

3. **Access Services:**
   - **Web Application**: `http://localhost`
   - **Swagger API Docs**: `http://localhost/swagger` (or `http://localhost:8080/swagger` in dev mode)

---

## 🛠️ Local Development

### Backend (.NET 8)
```bash
dotnet restore SecureMediaVault.slnx
dotnet build SecureMediaVault.slnx
```

### Frontend (Angular 18)
```bash
cd frontend
npm install
npm run build
```

---

## 🔄 CI / CD Workflows

- **`.github/workflows/ci.yml`**: Builds and tests backend and frontend on pull requests and pushes to `main`/`develop`.
- **`.github/workflows/cd-build-push.yml`**: Builds Docker container images matrix and pushes to GitHub Container Registry (`ghcr.io`).
- **`.github/workflows/cd-deploy.yml`**: SSH deployment to staging and production environments.
