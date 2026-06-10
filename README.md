# FulfillmentCenterService

Microserviço em **.NET 8** responsável por selecionar centros de fulfillment elegíveis, gerenciar capacidade operacional por data/modo e controlar reservas temporárias de capacidade para pedidos.

A solução expõe uma API HTTP minimalista, persiste dados em **PostgreSQL** via **Entity Framework Core**, publica eventos internos pelo padrão **Outbox** e executa um worker em background para expirar reservas pendentes.

---

## Sumário

- [Visão geral](#visão-geral)
- [Principais capacidades](#principais-capacidades)
- [Arquitetura](#arquitetura)
- [Stack técnica](#stack-técnica)
- [Modelo de domínio](#modelo-de-domínio)
- [Fluxos de negócio](#fluxos-de-negócio)
- [API HTTP](#api-http)
- [Contratos e enums](#contratos-e-enums)
- [Persistência](#persistência)
- [Eventos de outbox](#eventos-de-outbox)
- [Worker de expiração de reservas](#worker-de-expiração-de-reservas)
- [Configuração](#configuração)
- [Como executar localmente](#como-executar-localmente)
- [Health check e Swagger](#health-check-e-swagger)
- [Exemplos de chamadas](#exemplos-de-chamadas)
- [Validações e regras importantes](#validações-e-regras-importantes)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Comandos úteis](#comandos-úteis)
- [Observações para evolução](#observações-para-evolução)

---

## Visão geral

O `FulfillmentCenterService` centraliza decisões operacionais relacionadas a centros de fulfillment. Ele responde perguntas como:

- Quais centros podem atender um pedido de determinado vendedor, CEP, modalidade e perfil de pacote?
- Existe capacidade disponível em uma data operacional específica?
- Como reservar, confirmar ou liberar capacidade para um pedido?
- Como configurar a capacidade diária de um centro?
- Como alterar o status operacional de um centro?

O serviço utiliza dados de cobertura por faixa de CEP, vínculo de vendedor com centro, calendário operacional, capacidade disponível e características físicas aceitas por cada centro.

---

## Principais capacidades

- **Busca de candidatos de fulfillment** por vendedor, CEP de destino, modalidade e perfil do pacote.
- **Filtro de elegibilidade** por:
  - status ativo do centro;
  - matrícula ativa do vendedor no centro;
  - faixa de CEP coberta;
  - modalidade de fulfillment;
  - peso máximo, peso cúbico máximo e restrições de manuseio.
- **Resolução de janela operacional** considerando fuso horário, data operacional, abertura e cutoff.
- **Cálculo de unidades de capacidade** com base no maior valor entre peso físico e peso cúbico, com fator adicional para itens frágeis.
- **Ordenação de candidatos** por score operacional, utilização e prioridade de cobertura.
- **Configuração de capacidade** por centro, data e modalidade.
- **Reserva idempotente de capacidade** via header `Idempotency-Key`.
- **Confirmação de reserva**, convertendo capacidade reservada em capacidade consumida.
- **Liberação de reserva pendente**, devolvendo capacidade ao slot.
- **Expiração automática** de reservas pendentes após 15 minutos.
- **Publicação de eventos** via tabela de outbox.
- **Health check** de aplicação e banco de dados.

---

## Arquitetura

A solução segue uma separação simples em camadas:

```text
API HTTP
  ↓
Application Services
  ↓
Ports / Interfaces
  ↓
Infrastructure / EF Core / PostgreSQL
  ↓
Domain Entities + Outbox
```

### Camadas

| Camada | Responsabilidade |
| --- | --- |
| `Api` | Define endpoints HTTP e roteamento minimal API. |
| `Contracts` | Define DTOs de entrada e saída usados pela API. |
| `Application` | Orquestra casos de uso, validações e regras transacionais. |
| `Application/Ports` | Define interfaces para persistência e escrita de eventos. |
| `Domain` | Contém entidades, enums e regras invariantes do domínio. |
| `Infrastructure/Persistence` | Implementa repositórios e mapeamento EF Core. |
| `Infrastructure/Outbox` | Cria mensagens de evento persistidas em outbox. |
| `Infrastructure/Workers` | Executa processamento assíncrono de expiração de reservas. |

---

## Stack técnica

- **.NET 8 / ASP.NET Core Minimal APIs**
- **Entity Framework Core 8**
- **PostgreSQL** com provider `Npgsql.EntityFrameworkCore.PostgreSQL`
- **Swagger / OpenAPI** via `Swashbuckle.AspNetCore`
- **Health Checks** com validação do `DbContext`
- **BackgroundService** para expiração automática de reservas

---

## Modelo de domínio

### `FulfillmentCenter`

Representa um centro operacional. Campos principais:

- `Code`, `Name`, `Region` e `TimeZoneId`;
- `Status`;
- limites de peso físico e cúbico;
- suporte a itens frágeis e restritos.

Um centro só é elegível para busca quando está com status `Active`.

### `CenterCoverage`

Define a cobertura de um centro por faixa de CEP e modalidade.

- `PostalCodeFrom` e `PostalCodeTo`: intervalo numérico de CEP;
- `Mode`: modalidade atendida;
- `Priority`: prioridade usada no score de seleção.

### `SellerCenterEnrollment`

Define se um vendedor pode operar em determinado centro e modalidade.

- `SellerId`;
- `FulfillmentCenterId`;
- `Mode`;
- `IsActive`.

### `CenterOperationSchedule`

Define o calendário operacional de um centro para uma data e modalidade.

- `OperationDate`;
- `Mode`;
- `IsOpen`;
- `OpeningTime`;
- `CutoffTime`;
- `ClosingTime`.

### `CapacitySlot`

Representa a capacidade diária de um centro para uma modalidade.

- `TotalCapacityUnits`: capacidade total configurada;
- `ReservedCapacityUnits`: capacidade reservada, ainda não confirmada;
- `ConsumedCapacityUnits`: capacidade confirmada/consumida;
- `AvailableCapacityUnits`: calculada como `Total - Reserved - Consumed`;
- `UtilizationPercentage`: percentual de ocupação.

### `CapacityReservation`

Representa uma reserva temporária de capacidade para um pedido.

- criada com status `Pending`;
- expira em 15 minutos;
- pode ser confirmada enquanto estiver pendente e não expirada;
- pode ser liberada enquanto estiver pendente;
- pode expirar automaticamente pelo worker.

---

## Fluxos de negócio

### 1. Busca de centros candidatos

Endpoint: `POST /fulfillment-centers/candidates/search`

Fluxo resumido:

1. Valida vendedor, CEP e pacote.
2. Normaliza o CEP mantendo apenas dígitos e exige 8 dígitos.
3. Calcula unidades de capacidade necessárias:
   - `max(pesoKg, pesoCubicoKg)`;
   - aplica fator `1.5` para itens frágeis;
   - arredonda para cima;
   - mínimo de 1 unidade.
4. Busca centros elegíveis por vendedor, CEP, status ativo, cobertura e modalidade.
5. Filtra centros que não suportam peso, peso cúbico, item frágil ou item restrito.
6. Resolve a próxima data operacional aberta em até 14 dias.
7. Verifica capacidade disponível na data operacional resolvida.
8. Calcula score e ordena candidatos.

Score:

```text
score = prioridadeCobertura * 100
      + percentualUtilizacao
      + atrasoProcessamentoEmDias * 200
```

Quanto menor o score, mais prioritário é o candidato.

### 2. Configuração de capacidade

Endpoint: `PUT /fulfillment-centers/{fulfillmentCenterId}/capacity`

Fluxo resumido:

1. Valida o identificador do centro.
2. Confirma que o centro existe.
3. Cria um novo slot de capacidade para centro/data/modalidade ou reconfigura um existente.
4. Impede reduzir a capacidade abaixo do volume já reservado ou consumido.
5. Gera evento `FulfillmentCapacityConfigured` na outbox.

### 3. Alteração de status do centro

Endpoint: `PATCH /fulfillment-centers/{fulfillmentCenterId}/status`

Fluxo resumido:

1. Busca o centro.
2. Altera o status.
3. Atualiza `UpdatedAt`.
4. Gera evento `FulfillmentCenterStatusChanged` na outbox.

### 4. Criação de reserva de capacidade

Endpoint: `POST /capacity-reservations/`

Obrigatório informar o header:

```http
Idempotency-Key: <chave-unica-da-operacao>
```

Fluxo resumido:

1. Valida pedido, centro e unidades de capacidade.
2. Busca reserva existente pela `Idempotency-Key`.
3. Se existir, retorna a reserva existente.
4. Abre transação.
5. Tenta reservar capacidade de forma atômica no slot.
6. Cria reserva com status `Pending` e expiração em 15 minutos.
7. Gera evento `FulfillmentCapacityReserved` na outbox.
8. Salva alterações e confirma transação.

### 5. Confirmação de reserva

Endpoint: `POST /capacity-reservations/{reservationId}/confirm`

Fluxo resumido:

1. Busca a reserva.
2. Se já estiver confirmada, retorna o estado atual.
3. Valida que a reserva está pendente e não expirada.
4. Move capacidade de `ReservedCapacityUnits` para `ConsumedCapacityUnits`.
5. Atualiza a reserva para `Confirmed`.
6. Gera evento `FulfillmentCapacityConfirmed` na outbox.

### 6. Liberação de reserva

Endpoint: `POST /capacity-reservations/{reservationId}/release`

Fluxo resumido:

1. Busca a reserva.
2. Se já estiver `Released` ou `Expired`, retorna o estado atual.
3. Libera a capacidade reservada.
4. Atualiza a reserva para `Released`.
5. Gera evento `FulfillmentCapacityReleased` na outbox.

> Observação: a regra atual não permite liberar reservas já confirmadas por esse fluxo.

### 7. Expiração automática de reservas

O worker `ReservationExpirationWorker` roda a cada 30 segundos e processa até 100 reservas pendentes expiradas por ciclo.

Fluxo resumido:

1. Busca reservas `Pending` com `ExpiresAt <= UtcNow`.
2. Libera a capacidade reservada.
3. Atualiza a reserva para `Expired`.
4. Gera evento `FulfillmentCapacityReservationExpired` na outbox.
5. Salva alterações.

---

## API HTTP

### `GET /health`

Verifica a saúde da aplicação e do `FulfillmentDbContext`.

**Resposta esperada:** `200 OK` quando a aplicação e o banco estão saudáveis.

---

### `POST /fulfillment-centers/candidates/search`

Busca centros candidatos para atender um pacote.

**Request body:**

```json
{
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "destinationPostalCode": "01310-000",
  "mode": "Fulfillment",
  "package": {
    "weightKg": 2.5,
    "cubicWeightKg": 3.1,
    "isFragile": true,
    "isRestricted": false
  },
  "requestedAtUtc": "2026-06-10T12:00:00Z"
}
```

**Response `200 OK`:**

```json
[
  {
    "fulfillmentCenterId": "22222222-2222-2222-2222-222222222222",
    "code": "FC-SP01",
    "name": "Fulfillment Center São Paulo 01",
    "region": "SP",
    "mode": "Fulfillment",
    "processingDate": "2026-06-10",
    "cutoffAt": "2026-06-10T18:00:00-03:00",
    "availableCapacityUnits": 120,
    "utilizationPercentage": 37.5,
    "score": 137
  }
]
```

---

### `PUT /fulfillment-centers/{fulfillmentCenterId}/capacity`

Configura ou reconfigura a capacidade para um centro, data e modalidade.

**Request body:**

```json
{
  "operationDate": "2026-06-10",
  "mode": "Fulfillment",
  "totalCapacityUnits": 500
}
```

**Response:** `204 No Content`

---

### `PATCH /fulfillment-centers/{fulfillmentCenterId}/status`

Altera o status operacional de um centro.

**Request body:**

```json
{
  "status": "Maintenance"
}
```

**Response:** `204 No Content`

---

### `POST /capacity-reservations/`

Cria uma reserva temporária de capacidade.

**Headers:**

```http
Idempotency-Key: order-12345-reservation-v1
```

**Request body:**

```json
{
  "orderId": "33333333-3333-3333-3333-333333333333",
  "fulfillmentCenterId": "22222222-2222-2222-2222-222222222222",
  "operationDate": "2026-06-10",
  "mode": "Fulfillment",
  "requiredCapacityUnits": 4
}
```

**Response `201 Created`:**

```json
{
  "reservationId": "44444444-4444-4444-4444-444444444444",
  "orderId": "33333333-3333-3333-3333-333333333333",
  "fulfillmentCenterId": "22222222-2222-2222-2222-222222222222",
  "operationDate": "2026-06-10",
  "mode": "Fulfillment",
  "reservedCapacityUnits": 4,
  "status": "Pending",
  "expiresAt": "2026-06-10T12:15:00Z"
}
```

**Erros comuns:**

- `400 Bad Request` quando o header `Idempotency-Key` não é enviado.
- Exceção de negócio quando não há capacidade suficiente.

---

### `POST /capacity-reservations/{reservationId}/confirm`

Confirma uma reserva pendente e converte capacidade reservada em consumida.

**Response `200 OK`:** retorna o estado atualizado da reserva.

---

### `POST /capacity-reservations/{reservationId}/release`

Libera uma reserva pendente e devolve capacidade ao slot.

**Response `200 OK`:** retorna o estado atualizado da reserva.

---

## Contratos e enums

### Modalidades (`FulfillmentMode`)

| Valor | Descrição sugerida |
| --- | --- |
| `Fulfillment` | Operação completa de armazenagem/separação/expedição. |
| `CrossDocking` | Recebimento e redirecionamento sem armazenagem longa. |
| `DropOff` | Ponto de entrega/drop-off. |
| `Collection` | Operação de coleta. |

### Status do centro (`FulfillmentCenterStatus`)

| Valor | Uso |
| --- | --- |
| `Active` | Centro elegível para seleção. |
| `TemporarilyUnavailable` | Centro temporariamente indisponível. |
| `Maintenance` | Centro em manutenção. |
| `Inactive` | Centro inativo. |

### Status da reserva (`CapacityReservationStatus`)

| Valor | Uso |
| --- | --- |
| `Pending` | Reserva criada e aguardando confirmação ou liberação. |
| `Confirmed` | Reserva confirmada e capacidade consumida. |
| `Released` | Reserva liberada manualmente. |
| `Expired` | Reserva expirada automaticamente. |

---

## Persistência

A aplicação usa `FulfillmentDbContext` com PostgreSQL. As principais tabelas mapeadas são:

| Tabela | Entidade | Finalidade |
| --- | --- | --- |
| `fulfillment_centers` | `FulfillmentCenter` | Cadastro dos centros. |
| `center_coverages` | `CenterCoverage` | Coberturas por CEP e modalidade. |
| `seller_center_enrollments` | `SellerCenterEnrollment` | Vínculo vendedor-centro-modalidade. |
| `center_operation_schedules` | `CenterOperationSchedule` | Calendário operacional. |
| `capacity_slots` | `CapacitySlot` | Capacidade diária por centro/data/modalidade. |
| `capacity_reservations` | `CapacityReservation` | Reservas de capacidade. |
| `outbox_messages` | `OutboxMessage` | Eventos pendentes/processados pelo padrão outbox. |

### Índices e restrições relevantes

- `fulfillment_centers.Code` é único.
- `capacity_slots` possui índice único por centro, data e modalidade.
- `capacity_slots` possui check constraint para impedir alocação maior que capacidade total.
- `capacity_reservations.IdempotencyKey` é único.
- `capacity_reservations` possui índice por status e data de expiração.
- `center_operation_schedules` possui índice único por centro, data e modalidade.
- `seller_center_enrollments` possui índice único por vendedor, centro e modalidade.

---

## Eventos de outbox

O serviço grava eventos na tabela `outbox_messages`. Cada mensagem contém:

- `Id`;
- `EventType`;
- `PayloadJson`;
- `OccurredAt`;
- `ProcessedAt`.

Eventos gerados atualmente:

| Evento | Quando é gerado |
| --- | --- |
| `FulfillmentCapacityConfigured` | Ao configurar/reconfigurar capacidade. |
| `FulfillmentCenterStatusChanged` | Ao alterar status de um centro. |
| `FulfillmentCapacityReserved` | Ao criar reserva de capacidade. |
| `FulfillmentCapacityConfirmed` | Ao confirmar reserva. |
| `FulfillmentCapacityReleased` | Ao liberar reserva. |
| `FulfillmentCapacityReservationExpired` | Ao expirar reserva pendente. |

> Importante: este projeto grava as mensagens na outbox, mas não inclui um publicador externo para marcar `ProcessedAt` ou entregar eventos a um broker.

---

## Worker de expiração de reservas

`ReservationExpirationWorker` é registrado como `HostedService` e executa continuamente durante a vida da aplicação.

Comportamento:

- intervalo de execução: 30 segundos;
- lote máximo: 100 reservas por ciclo;
- critério: reservas `Pending` com `ExpiresAt` menor ou igual ao horário UTC atual;
- ação: libera capacidade, marca reserva como `Expired` e grava evento de outbox.

---

## Configuração

Arquivo principal: `appsettings.json`.

```json
{
  "ConnectionStrings": {
    "FulfillmentDb": "Host=localhost;Port=5432;Database=fulfillment_center;Username=postgres;Password=postgres"
  }
}
```

### Variáveis de ambiente

Em ambientes produtivos, prefira sobrescrever a connection string via variável de ambiente:

```bash
ConnectionStrings__FulfillmentDb="Host=<host>;Port=5432;Database=<database>;Username=<user>;Password=<password>"
```

### Ambientes

- `Development`: habilita Swagger e Swagger UI.
- Outros ambientes: Swagger não é habilitado pelo código atual.

---

## Como executar localmente

### Pré-requisitos

- .NET SDK 8.0 ou superior.
- PostgreSQL acessível localmente ou via container.
- Banco de dados criado com o nome configurado na connection string.

### 1. Restaurar dependências

```bash
dotnet restore
```

### 2. Configurar PostgreSQL

Exemplo com Docker:

```bash
docker run --name fulfillment-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=fulfillment_center \
  -p 5432:5432 \
  -d postgres:16
```

### 3. Criar ou atualizar o schema

O repositório atual não possui pasta de migrations versionada. Para gerar migrations EF Core, use:

```bash
dotnet ef migrations add InitialCreate
```

Depois aplique no banco:

```bash
dotnet ef database update
```

> Caso o comando `dotnet ef` não esteja disponível, instale a ferramenta com `dotnet tool install --global dotnet-ef` ou use uma instalação local via manifest de ferramentas.

### 4. Executar a aplicação

```bash
dotnet run
```

A porta pode variar conforme o perfil de execução. O arquivo `FulfillmentCenterService.http` usa `http://localhost:5039` como endereço local de referência.

---

## Health check e Swagger

### Health check

```bash
curl http://localhost:5039/health
```

### Swagger

Disponível apenas em ambiente `Development`:

```text
http://localhost:5039/swagger
```

---

## Exemplos de chamadas

### Buscar candidatos

```bash
curl -X POST http://localhost:5039/fulfillment-centers/candidates/search \
  -H "Content-Type: application/json" \
  -d '{
    "sellerId": "11111111-1111-1111-1111-111111111111",
    "destinationPostalCode": "01310-000",
    "mode": "Fulfillment",
    "package": {
      "weightKg": 2.5,
      "cubicWeightKg": 3.1,
      "isFragile": true,
      "isRestricted": false
    },
    "requestedAtUtc": "2026-06-10T12:00:00Z"
  }'
```

### Configurar capacidade

```bash
curl -X PUT http://localhost:5039/fulfillment-centers/22222222-2222-2222-2222-222222222222/capacity \
  -H "Content-Type: application/json" \
  -d '{
    "operationDate": "2026-06-10",
    "mode": "Fulfillment",
    "totalCapacityUnits": 500
  }'
```

### Alterar status do centro

```bash
curl -X PATCH http://localhost:5039/fulfillment-centers/22222222-2222-2222-2222-222222222222/status \
  -H "Content-Type: application/json" \
  -d '{
    "status": "Maintenance"
  }'
```

### Criar reserva

```bash
curl -X POST http://localhost:5039/capacity-reservations/ \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: order-33333333-reservation-v1" \
  -d '{
    "orderId": "33333333-3333-3333-3333-333333333333",
    "fulfillmentCenterId": "22222222-2222-2222-2222-222222222222",
    "operationDate": "2026-06-10",
    "mode": "Fulfillment",
    "requiredCapacityUnits": 4
  }'
```

### Confirmar reserva

```bash
curl -X POST http://localhost:5039/capacity-reservations/44444444-4444-4444-4444-444444444444/confirm
```

### Liberar reserva

```bash
curl -X POST http://localhost:5039/capacity-reservations/44444444-4444-4444-4444-444444444444/release
```

---

## Validações e regras importantes

### CEP

- O CEP é normalizado mantendo apenas dígitos.
- Deve conter exatamente 8 dígitos.

### Pacote

- `WeightKg` deve ser maior que zero.
- `CubicWeightKg` não pode ser negativo.
- Itens frágeis exigem que o centro suporte `SupportsFragileItems`.
- Itens restritos exigem que o centro suporte `SupportsRestrictedItems`.

### Capacidade

- Não é possível configurar capacidade negativa.
- Não é possível reduzir a capacidade total abaixo da soma de reservada + consumida.
- Reserva só é criada se houver capacidade disponível suficiente.
- Confirmação exige reserva pendente e não expirada.
- Liberação manual não aceita reserva já confirmada.

### Idempotência

A criação de reserva usa `Idempotency-Key`. Se uma chamada for repetida com a mesma chave, o serviço retorna a reserva já existente em vez de criar uma nova.

### Calendário operacional

- A busca avalia a data local do centro com base no `TimeZoneId`.
- Se o horário local do dia atual passou do `CutoffTime`, o dia é ignorado.
- A busca procura uma janela aberta por até 14 dias a partir da data local inicial.

---

## Estrutura de pastas

```text
.
├── Api/
│   └── FulfillmentEndpoints.cs
├── Application/
│   ├── CapacityManagementService.cs
│   ├── CapacityReservationService.cs
│   ├── CandidateSearchService.cs
│   ├── OperationalCalendarService.cs
│   └── Ports/
├── Contracts/
│   ├── CandidateContracts.cs
│   ├── CapacityContracts.cs
│   └── ReservationContracts.cs
├── Domain/
│   ├── CapacityReservation.cs
│   ├── CapacitySlot.cs
│   ├── CenterCoverage.cs
│   ├── CenterOperationSchedule.cs
│   ├── FulfillmentCenter.cs
│   ├── FulfillmentMode.cs
│   ├── FulfillmentCenterStatus.cs
│   ├── ReservationStatus.cs
│   └── SellerCenterEnrollment.cs
├── Infrastructure/
│   ├── Outbox/
│   ├── Persistence/
│   └── Workers/
├── Program.cs
├── appsettings.json
└── FulfillmentCenterService.csproj
```

---

## Comandos úteis

### Build

```bash
dotnet build
```

### Executar

```bash
dotnet run
```

### Restaurar pacotes

```bash
dotnet restore
```

### Criar migration

```bash
dotnet ef migrations add <NomeDaMigration>
```

### Aplicar migrations

```bash
dotnet ef database update
```

---

## Observações para evolução

Possíveis melhorias futuras:

- Adicionar migrations EF Core versionadas no repositório.
- Adicionar testes unitários para regras de domínio e serviços de aplicação.
- Adicionar testes de integração com PostgreSQL.
- Implementar publicador de outbox para broker externo.
- Padronizar tratamento de exceções de negócio em respostas HTTP estruturadas.
- Adicionar autenticação/autorização nos endpoints administrativos.
- Expor endpoints de leitura para reservas, capacidade e centros.
- Adicionar seeds ou scripts SQL para dados iniciais de centros, coberturas e calendários.
- Adicionar observabilidade com métricas, tracing e logs estruturados.
