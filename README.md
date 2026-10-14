# CampusEats
Full-stack demo for SE3090 Lab 11.

| Folder | What |
|---|---|
| `campuseats-web` | React + Vite frontend |
| `CampusEats.Api` | ASP.NET Core 8 API |

## Environment variables
| Tier | Name | Example |
|---|---|---|
| Frontend | `VITE_API_URL` | `https://your-api.onrender.com` |
| Backend | `ConnectionStrings__Default` | set on the host (secret) |
| Backend | `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` | set on the host |
| Backend | `Cors__AllowedOrigins` | `https://your-site.vercel.app` |

Secrets are never committed. Locally use `dotnet user-secrets`.
