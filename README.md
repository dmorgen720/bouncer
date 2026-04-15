# Bouncer — Recruiter Email Triage

A local AI tool that sits between your inbox and your attention.
Automatically reads, assesses, and drafts replies to recruiter emails — you just click one button.

Built as a proof of concept in response to [this LinkedIn post](<!-- add your post URL here -->).

---

## How it works

Two AI agents work in sequence:

1. **Classifier** — reads the subject, sender, and email preview. Decides whether this is a real job offer or just noise (newsletters, job alerts, platform notifications). Fast and cheap — no full email fetch needed.

2. **Assessor** — takes the genuine ones, reads the full email body plus any PDF or Word attachments, and evaluates against your personal profile and criteria (location, role type, seniority, remote policy). Drafts both an acceptance and a decline reply.

You see a card. You click one button. Done.

---

## Tech stack

- **.NET 10** + **Blazor Server** — UI
- **Microsoft Agent Framework** (`Microsoft.Extensions.AI`) — AI abstraction layer
- **Ollama** + **OllamaSharp** — local LLM inference (no data leaves your machine)
- **Microsoft Graph API** — connects to your Office 365 / Exchange mailbox
- **SQLite** + **EF Core** — local persistence
- **MudBlazor** — UI components

Swapping models is a one-line config change. OpenAI is also supported if you prefer cloud inference.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com/) running locally with a model pulled (e.g. `ollama pull gemma4:e4b`)
- An **Azure AD app registration** with Microsoft Graph permissions:
  - `Mail.Read`
  - `Mail.ReadWrite`
  - `Mail.Send`

---

## Setup

### 1. Clone the repo

```bash
git clone https://github.com/your-username/bouncer.git
cd bouncer
```

### 2. Configure credentials

Copy `.env.example` to `.env` and fill in your values:

```bash
cp src/LinkedInAutoReply/.env.example src/LinkedInAutoReply/.env
```

```env
Bouncer__Password=your-login-password

Graph__TenantId=your-azure-tenant-id
Graph__ClientId=your-azure-app-client-id
Graph__ClientSecret=your-azure-app-client-secret
Graph__UserId=your-email@yourdomain.com
```

### 3. Configure your profile

Edit `src/LinkedInAutoReply/Prompts/assessment.md` and replace the `[PLACEHOLDERS]` with your actual profile, location preferences, role criteria, and name.

### 4. Configure the AI provider

In `appsettings.json`, set your preferred provider and model:

```json
"AI": {
  "Provider": "Ollama",
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelId": "gemma4:e4b"
  }
}
```

For OpenAI, set `"Provider": "OpenAI"` and add your API key.

### 5. Run

```bash
cd src/LinkedInAutoReply
dotnet run
```

Navigate to `https://localhost:5001`, log in with your configured password, and hit **Scan Now**.

---

## Azure AD App Registration

In the [Azure Portal](https://portal.azure.com):

1. Create a new App Registration
2. Under **Certificates & secrets**, create a client secret
3. Under **API permissions**, add Microsoft Graph application permissions:
   - `Mail.Read`
   - `Mail.ReadWrite`
   - `Mail.Send`
4. Grant admin consent

---

## Project structure

```
src/LinkedInAutoReply/
├── Components/         Blazor UI pages and layout
├── Models/             Domain models and settings
├── Prompts/            LLM prompt files (edit these to match your profile)
│   ├── assessment.md   Full assessor prompt — personalise this
│   └── classifier.md   Classifier prompt — no changes needed
├── Services/           Core pipeline services
│   ├── MailWorker.cs         Background polling loop
│   ├── JobOfferClassifier.cs Stage 1 — LLM classifier
│   ├── RecruitmentAssessor.cs Stage 2 — LLM assessor
│   ├── GraphMailService.cs   Microsoft Graph inbox integration
│   ├── GraphDraftService.cs  Draft reply via Graph createReply
│   ├── AttachmentTextExtractor.cs PDF + Word extraction
│   └── LinkedInMessageParser.cs  HTML email body parser
└── Data/               EF Core DbContext + migrations
```

---

## License

MIT
