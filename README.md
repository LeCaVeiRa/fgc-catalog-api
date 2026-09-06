# 🎮 FCG Catalog API

API REST desenvolvida em **.NET 8** como parte do Tech Challenge da pós-graduação **Arquitetura de Sistemas .NET – FIAP**.

O projeto representa o microsserviço de **Catálogo** da plataforma de games educacionais (**FCG – FIAP Cloud Games**): CRUD de jogos, biblioteca de jogos por usuário e ponto de entrada do fluxo de compra (`Order`).

---

## 📌 Objetivo do Projeto

Arquitetura de microsserviços orientada a eventos, garantindo:

- Autonomia de código e ciclo de vida (repositório isolado)
- Comunicação assíncrona utilizando Mensageria (**RabbitMQ** / **Amazon MQ** em produção)
- Persistência de dados isolada (SQLite para `Game`/`User`, DynamoDB para `Order` e log de eventos)
- Cache com **Redis**
- Containerização com **Docker**
- Base escalável para orquestração em Kubernetes

---

## 🛠️ Tecnologias Utilizadas

- **.NET 8** / **ASP.NET Core Web API**
- **Entity Framework Core** + **SQLite**
- **MassTransit** + **RabbitMQ** (Amazon MQ/AMQPS em produção)
- **Redis** (`IDistributedCache`, cache de `GET /games`)
- **AWS DynamoDB** (persistência de `Order` e log de eventos de domínio)
- **Prometheus** (`prometheus-net.AspNetCore`, métricas em `/metrics`)
- **JWT Bearer Authentication** (validação apenas — os tokens são emitidos pela Users API)
- **Docker**
- **Swagger / OpenAPI**
- **ILogger** para logs estruturados

---

## 🧱 Arquitetura

Clean Architecture / DDD, com quatro projetos sob `src/`:

- **`Fgc.Catalog.Api`** — Controllers, `Consumers/` (um consumidor não registrado, ver nota abaixo), middlewares, `Program.cs` (composição, incluindo o wiring de MassTransit/RabbitMQ), `appsettings*.json`.
- **`Fgc.Catalog.Application`** — Serviços de negócio, `Interfaces`, `DTOS`, `Events/`, e os `Consumers/` efetivamente registrados no `Program.cs`.
- **`Fgc.Catalog.Domain`** — Entidades `Game`, `Order`, `User` (réplica local sincronizada por evento), `UserLibrary`, exceções de domínio.
- **`Fgc.Catalog.Infrastructure`** — `DbContext` (EF Core), `Repositories` (inclui o repositório de `Order` em DynamoDB), `Configuration/`, `Migrations/`.

---

## 📁 Estrutura de Pastas

```text
Fgc.Catalog/
├── src/
│   ├── Fgc.Catalog.Api/
│   │   ├── Controllers/
│   │   ├── Consumers/
│   │   ├── Middlewares/
│   │   ├── Program.cs
│   │   └── appsettings.json
│   ├── Fgc.Catalog.Application/
│   │   ├── Services/
│   │   ├── Interfaces/
│   │   ├── DTOS/
│   │   ├── Events/
│   │   └── Consumers/
│   ├── Fgc.Catalog.Domain/
│   │   └── Entities/         # Game, Order, User, UserLibrary
│   └── Fgc.Catalog.Infrastructure/
│       ├── Persistence/
│       ├── Repositories/
│       ├── Configuration/
│       └── Migrations/
├── tests/
│   ├── Fgc.Catalog.Tests/                 # xUnit + Moq, unitários
│   └── Fgc.Catalog.IntegrationTests/      # Infraestructure/, WebApplicationFactory
├── k8s/
├── LocalPackages/                         # .nupkg do Fgc.MessageContracts
├── nuget.config
└── Dockerfile
```

> Este repositório não tem `docker-compose.yml` próprio — a orquestração local roda a partir de `fgc-orchestration/`.

---

## 🔐 Segurança

* Autenticação via **JWT Bearer**, validado (não emitido) por este serviço.
* `[Authorize]` / `[Authorize(Roles = "Admin")]`.
* Middleware global para tratamento de exceções, logs estruturados via `ILogger`.

```http
Authorization: Bearer {token}
```

### Níveis de acesso

* **User** — autentica na Users API, consulta o catálogo, realiza pedidos (`Order`).
* **Admin** — além do acima, cadastra/atualiza/remove jogos.

---

## 📨 Mensageria e Eventos

* **Produtor:** ao criar um pedido (`POST /orders`), publica `OrderPlacedEvent`.
* **Consumidores registrados** em `Program.cs`:
  * `Fgc.Catalog.Application.Consumers.UserCreatedEventConsumer` (fila `catalog-user-created-queue`) — consome `UserCreatedEvent`.
  * `Fgc.Catalog.Application.Consumers.PaymentProcessedEventConsumer` (fila `catalog-payment-processed-queue`) — consome `PaymentProcessedEvent`; se `Approved`, adiciona o jogo à biblioteca do usuário.
* Ambos os eventos consumidos vêm do pacote compartilhado `Fgc.MessageContracts`.
* Existe também `Fgc.Catalog.Api.Consumers.UserCreatedEventConsumer`, que não está registrado no `Program.cs` — não é o código que roda em produção.

---

## 🔗 Endpoints Principais

### `GameController` (`/games`)
* `GET /games` — lista jogos.
* `GET /games/{id}` — busca jogo por id.
* `POST /games` (Admin) — cria jogo.
* `PUT /games/{id}` (Admin) — atualiza jogo.
* `DELETE /games/{id}` (Admin) — remove jogo.

### `OrderController` (`/orders`)
* `POST /orders` — cria um pedido de compra (dispara `OrderPlacedEvent`).
* `GET /orders/{id:guid}` — busca pedido por id.

---

## 📘 Documentação e Testes

Swagger em `/swagger`. Testes: `Fgc.Catalog.Tests` (unitários) e `Fgc.Catalog.IntegrationTests`.

---

## ⚠️ Tratamento de Erros e 🪵 Logs

Middleware global de exceções (400/404/500, JSON padronizado); logs estruturados via `ILogger`; métricas Prometheus em `/metrics`.

---

## 🗄️ Banco de Dados

* **SQLite** via EF Core para `Game` e `User` (réplica local), com migrations em `Fgc.Catalog.Infrastructure/Migrations`.
* **AWS DynamoDB** para `Order` (tabela `Orders`, sem EF Core) e para o log de eventos de domínio.
* **Redis** como cache de leitura em `GET /games`.

---

## 📦 Pacote `Fgc.MessageContracts`

Referenciado via NuGet local (`nuget.config` aponta para `./LocalPackages`), versão **1.0.3** em `Fgc.Catalog.Api` e `Fgc.Catalog.Application`. `LocalPackages/` já contém o `.nupkg` correspondente.

---

## ▶️ Como Executar o Projeto

### Pré-requisitos

* .NET SDK 8+
* Docker (para RabbitMQ, via `fgc-orchestration/`)

### Variáveis de Ambiente (`appsettings.json`)

* `ConnectionStrings:CatalogDb`
* `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` (mesmos valores da Users API)
* `RabbitMq:Host`, `RabbitMq:Port`, `RabbitMq:VirtualHost`, `RabbitMq:Username`, `RabbitMq:Password`, `RabbitMq:UseSsl` — é essa a seção efetivamente lida em runtime (o `appsettings.json` também contém uma seção `MassTransit`, que não é usada pelo código).

### Execução via Docker

Não há `docker-compose.yml` neste repositório; suba a stack completa a partir de `fgc-orchestration/`:

```bash
cd ../fgc-orchestration
docker-compose up -d --build
```

### Execução Local

```bash
dotnet restore
dotnet ef database update --project src/Fgc.Catalog.Infrastructure --startup-project src/Fgc.Catalog.Api
dotnet run --project src/Fgc.Catalog.Api
```

Acesse: `http://localhost:5070/swagger`.

---

## ☸️ Kubernetes

Manifestos em `k8s/` (`deployment.yaml`, `service.yaml`, `configmap.yaml`, `secret.yaml`). Aplicar com `kubectl apply -f k8s/`.

---

## 👥 Squad 8 – Turma 12NETT

**Integrantes**

* Yan Santos Wendt
* Ronnam de Lima da Silva
