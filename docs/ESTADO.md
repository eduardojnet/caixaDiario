# Estado do Projeto

**Documento vivo.** Atualizado ao fim de cada bloco de trabalho. Descreve o que existe, o que falta e o que está decidido, sem otimismo.

**Última atualização:** 2026-10-12 (Bloco 4 concluído)
**Build:** `dotnet build CaixaDiario.sln`: 0 avisos, 0 erros (`TreatWarningsAsErrors`, `AnalysisMode=Recommended`)
**Testes:** 46 passando, 0 falhas (domínio 20, arquitetura 4, integração 22 com PostgreSQL e RabbitMQ reais via Testcontainers); carga k6 a 50 req/s com 0,00% de falha

---

## 1. Identificação

| | |
|---|---|
| Produto | **CaixaDiario** |
| Repositório | https://github.com/eduardojnet/caixaDiario (público) |
| Propósito | Desafio técnico de Arquiteto de Soluções |
| Domínio | Fluxo de caixa diário de um comerciante: lançamentos de débito e crédito e saldo diário consolidado. Não é conta corrente bancária |
| Teto de tempo | 5 horas de trabalho efetivo (300 min) |
| Plataforma prevista | .NET 10, PostgreSQL 17, broker a decidir no ADR-0002 |
| Decisor | Eduardo J. G. do Carmo |

---

## 2. Requisitos obrigatórios do enunciado

| # | Requisito | Estado |
|---|---|---|
| O1 | Mapeamento de domínios funcionais e capacidades de negócio | Atendido: `docs/01-capacidades-de-negocio.md`, `docs/02-dominios-funcionais.md` |
| O2 | Refinamento de requisitos funcionais e não funcionais | Atendido: `docs/03-requisitos.md` (RF-01 a RF-10, RNF-01 a RNF-07, restrições, premissas, drivers, QA-01 a QA-05) |
| O3 | Desenho da solução completo (Arquitetura Alvo) | Atendido: `docs/04-arquitetura-alvo.md`, `docs/diagrams/` (C1, C2, sequência), `docs/contratos/` |
| O4 | Justificativa de ferramentas, tecnologias e tipo de arquitetura | Atendido: `docs/adr/ADR-0001`, `docs/adr/ADR-0002`, aceitos no G2 |
| O5 | Linguagem dominada | Atendido: C# / .NET 10 (SDK 10.0.401) |
| O6 | Testes | Atendido: `tests/` (46 testes verdes) e `tests/carga/consolidado-50rps.js` (executado) |
| O7 | README: como funciona e como rodar localmente | Pendente (Bloco 5) |
| O8 | Repositório público no GitHub | Publicado: https://github.com/eduardojnet/caixaDiario, push a cada bloco desde o G2 |
| O9 | Toda documentação no repositório | Em andamento: Blocos 1 a 4 em `docs/`; README no Bloco 5 |

> O enunciado descarta o teste se algum requisito obrigatório não for minimamente atendido. Nenhum item desta tabela pode terminar como "Pendente".

---

## 3. Decisões e portões

| Item | Estado |
|---|---|
| ADR-0001 estilo arquitetural | Aceito: dois serviços, isolamento de falha e não volume |
| ADR-0002 plataforma, armazenamento e integração | Aceito: .NET 10, PostgreSQL 17 por serviço, Dapper, outbox + RabbitMQ |
| G1 pré-voo | Não acionado: as cinco verificações responderam (2026-10-12 19:55) |
| G2 aprovação dos ADRs e autorização de push | G2 aprovado em 2026-10-12 21:06 (Eduardo: "Considere todos os ADR aprovados") |

---

## 4. Implementado

- Esqueleto do repositório: `Directory.Build.props` (.NET 10, `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, `NuGetAudit`), `global.json` (Microsoft Testing Platform), `.editorconfig`, `.gitignore`.
- Documentação: `docs/01` a `docs/04`, ADR-0001 e ADR-0002 (aceitos), C1, C2, sequência, contrato `LancamentoRegistrado` v1 com exemplo JSON.
- `CaixaDiario.Lancamentos.Dominio`: `Lancamento`, `TipoLancamento`, validação (valor positivo com duas casas, tipo, data futura recusada, estorno de tipo oposto e mesmo valor). Sem dependência externa.
- `CaixaDiario.Lancamentos.Api`: `POST` com `Idempotency-Key` obrigatório (201, 200 com `Idempotency-Replayed`, 422), estorno, `GET` do dia, outbox na mesma transação, despachante com espera crescente, publicação com confirmação e `mandatory`, `/health/live` e `/health/ready` (só o próprio banco), logs JSON.
- `CaixaDiario.Consolidado.Api`: projetor idempotente, consulta com `atualizadoAte` e `defasado`, cache de 2 s, limitador token bucket (200/s, fila 50, excedente 429), estado do projetor medido pela própria fila.
- Esquemas: `db/lancamentos` (ledger append-only, chave única de idempotência, estorno único, outbox, papel com `SELECT`/`INSERT` e `UPDATE` só em `outbox.publicado_em`) e `db/consolidado` (`saldo_diario`, `lancamentos_aplicados`, `estado_projecao`).
- `docker-compose.yml`: 2 APIs, 2 PostgreSQL 17, RabbitMQ 4.1.8; `lancamentos` depende só do próprio banco.
- Testes: domínio, arquitetura (NetArchTest: Lancamentos não referencia Consolidado; sem assembly compartilhado), RNF-01 sem Consolidado e sem broker, idempotência sequencial e concorrente, estorno, imutabilidade por privilégio, projetor idempotente, reconciliação ponta a ponta, contrato nos dois lados.
- RNF medidos: RNF-01 (teste e demonstração no compose), RNF-02 e RNF-03 (k6: 3001 req a 50/s, 0,00% de falha, p95 1,56 ms), RNF-04 (reconciliação), RNF-06 (privilégio), RNF-07 (idempotência).

## 5. Especificado, não implementado

- RNF-05: meta de 5 s mantida como proposta `[NVI]`. A marca `defasado` está implementada; a defasagem em operação normal não foi medida com instrumento (observada abaixo de 1 s nas demonstrações, sem registro sistemático).
- Observabilidade: métricas de outbox e de defasagem, traço OpenTelemetry (`docs/04-arquitetura-alvo.md` §4.7). Implementado só log JSON e verificações de saúde.
- Segurança de integração: credencial de broker por serviço, TLS, OAuth2 client credentials na API (§4.8). Ambiente local usa um usuário só no broker e não autentica a API.
- Integrações com maquininha e banco (C1).
- Migração versionada: esquemas aplicados por script de inicialização do contêiner (cartão 39 no backlog).

## 6. Questões abertas e lacunas

| ID | Questão | Conduta provisória | Origem |
|---|---|---|---|
| L-01 | Números do PacioliBank no texto de citação divergiam do repositório (95% e 87% declarados; 94,11% e 86,96% medidos) | Texto corrigido no CLAUDE.md §5.3; reconferir no Bloco 5 | Verificação de 2026-10-12 |
| L-02 | Plano original não tinha ADR de tecnologia (requisito O4) | ADR-0002 incluído no Bloco 2 | Verificação de 2026-10-12 |
| QA-01 a QA-05 | Questões de negócio do enunciado | Condutas provisórias em `docs/03-requisitos.md` §7 | Bloco 1 |
| PR-01 a PR-04 | Premissas com impacto declarado | `docs/03-requisitos.md` §5 | Bloco 1 |
| L-03 | Eventos publicados antes de existir a fila do Consolidado seriam descartados pela exchange fanout | Corrigido: publicação com `mandatory`; sem fila, o evento fica na outbox. Achado pelo teste de reconciliação | Bloco 4 |
| L-04 | `defasado` baseado só no atraso do último evento ficaria verdadeiro para sempre depois de recuperar um acúmulo | Corrigido: estado do projetor (conexão e fila) entra no cálculo. Achado na demonstração do RNF-01 | Bloco 4 |
| L-05 | O Consolidado não enxerga a outbox de Lancamentos: com o broker fora, ele sabe que está desconectado, mas não quantos eventos esperam | Declarado; evolução: batimento periódico do despachante. Não muda o RNF-01 | Bloco 4 |

---

## 7. Fila de execução

| Bloco | Teto (min) | Início | Fim | Gasto | Estado | Critério de conclusão |
|---|---|---|---|---|---|---|
| 0. Pré-voo e repositório | 10 | 19:55 | 19:57 | 2 | Concluído | Ferramentas respondem; remoto configurado |
| 1. Fronteira e requisitos | 65 | 21:00 | 21:03 | 3 | Concluído | Dois serviços nominais no mapa; RNF-01 e RNF-02 com meta e medição |
| 2. Decisões | 40 | 21:04 | 21:06 | 2 | Concluído | Frase volume x isolamento no ADR-0001; G2 aprovado |
| 3. Arquitetura Alvo | 35 | 21:07 | 21:11 | 4 | Concluído | C2 renderiza; sobrevivência de Lancamentos visível |
| 4. Código e testes | 80 | 21:12 | 21:31 | 19 | Concluído | Compose sobe; POST 201; saldo confere; testes passam; POST 201 com Consolidado parado |
| 5. README e fechamento | 40 | | | | Pendente | Clone limpo sobe só com o README |
| Reserva | 30 | | | | Disponível | Consumida apenas pelo Bloco 4 ou por revisão final |
| **Total** | **300** | | | **30** | | |

---

## 8. Histórico

| Data | Evento |
|---|---|
| 2026-10-12 | Acessos verificados: repositório GitHub público e vazio com permissão de escrita; PacioliBank legível; pasta local com leitura e escrita |
| 2026-10-12 | Instruções de execução geradas (CLAUDE.md local, skill `/proximo`) e estado inicial registrado |
| 2026-10-12 | Bloco 0, pré-voo: `dotnet --list-sdks` → 10.0.401; `docker version` → cliente e servidor 29.8.2; `docker compose version` → v5.6.0; `git --version` → 2.43.0; `gh` ausente, acesso ao GitHub verificado por SSH (`ssh -T git@github.com` autentica `eduardojnet`) e `git ls-remote` HTTPS no remoto vazio retorna sem erro |
| 2026-10-12 | Bloco 0: remoto `origin` em HTTPS, conforme o plano; se o push HTTPS falhar por credencial após o G2, trocar para `git@github.com:eduardojnet/caixaDiario.git` (SSH verificado). Decisão reversível |
| 2026-10-12 | Bloco 0: `Directory.Build.props` e `template.md` adaptados do PacioliBank, com comentários e IDs de requisito daquele projeto removidos (regra 13); `global.json` idêntico |
| 2026-10-12 | Bloco 0: identidade git local do repositório igual à do PacioliBank (`Eduardo J. Net <eduardojnet@outlook.com>`), pois `user.name` global não está definido |
| 2026-10-12 | 2026-10-12 21:02 cartão 07 concluído: docs/01-capacidades-de-negocio.md com cadeia de valor de 5 etapas, 4 capacidades nível 1 e 11 nível 2, todas com criticidade e domínio |
| 2026-10-12 | 2026-10-12 21:02 cartão 08 concluído: docs/02-dominios-funcionais.md nomeia Controle de Lançamentos (núcleo, crítico) e Consolidado Diário (suporte, sacrificável); fronteiras, assimetria, dependência unidirecional e glossário de 13 termos |
| 2026-10-12 | 2026-10-12 21:02 cartão 09 concluído: docs/03-requisitos.md §2 com RF-01 a RF-10, cada um com domínio e capacidade |
| 2026-10-12 | 2026-10-12 21:02 cartão 10 concluído: docs/03-requisitos.md §3 com RNF-01 a RNF-07, cada um com métrica, meta e medição; RNF-05 marcado [NVI] |
| 2026-10-12 | 2026-10-12 21:02 cartão 11 concluído: docs/03-requisitos.md §7 com QA-01 a QA-05, conduta provisória e o que muda se respondida; restrições, premissas e drivers em seções próprias |
| 2026-10-12 | Bloco 1: critério verificado: `docs/02` nomeia Controle de Lançamentos e Consolidado Diário (8 ocorrências); RNF-01 e RNF-02 com métrica, meta e medição em `docs/03` §3; §11 aplicada (01 em N1 sem tecnologia, 02 em N2-N3, 03 separa requisito, restrição, premissa, driver e QA) |
| 2026-10-12 | 2026-10-12 21:04 cartão 12 concluído: ADR-0001 com critérios datados 2026-10-12 antes das opções, conta 50 x 86.400 ≈ 4,3 milhões, frase literal 'O volume não justifica a separação; o isolamento de falha justifica' (linha encontrada por grep), comparação com o PacioliBank, consequências negativas e gatilho |
| 2026-10-12 | 2026-10-12 21:04 cartão 13 concluído: ADR-0002 com cinco temas, cada um com alternativa rejeitada e gatilho; transporte A/B/C decidido pelo critério 1; docs/adr/README.md com índice |
| 2026-10-12 | 2026-10-12 21:06 cartão 14 concluído: G2 aprovado por Eduardo; ADR-0001 e ADR-0002 passam a Aceito com redação de fato; nenhuma marca da avaliadora em docs/; primeiro push de main |
| 2026-10-12 | Push HTTPS falhou por credencial (`could not read Username`); remoto trocado para `git@github.com:eduardojnet/caixaDiario.git` e push concluído (origin/main = 36474e7) |
| 2026-10-12 | TickTick sincronizado: cartões 04 a 14 movidos para Concluído, checklists marcados |
| 2026-10-12 | 2026-10-12 21:10 cartão 15 concluído: docs/diagrams/c1-contexto.md e README.md da convenção; mermaid-cli 11.4.2 renderiza (rc=0) |
| 2026-10-12 | 2026-10-12 21:10 cartão 16 concluído: docs/diagrams/c2-conteineres.md renderiza (rc=0, PNG inspecionado); nenhuma seta da zona sacrificável para a crítica; tabela de falhas explica a sobrevivência |
| 2026-10-12 | 2026-10-12 21:10 cartão 17 concluído: docs/diagrams/sequencia-registro-e-projecao.md renderiza (rc=0); passos 4 a 6 mostram lançamento e evento na mesma transação |
| 2026-10-12 | 2026-10-12 21:10 cartão 18 concluído: docs/contratos/lancamento-registrado.v1.md com 8 campos, exemplo JSON versionado em arquivo próprio, regra de versão e semântica ao menos uma vez |
| 2026-10-12 | 2026-10-12 21:10 cartão 19 concluído: docs/04-arquitetura-alvo.md nas seis camadas da §11.5, degradação, um parágrafo de observabilidade e um de segurança |
| 2026-10-12 | Bloco 3: validação Mermaid com `minlag/mermaid-cli:11.4.2` em contêiner (imagem baixada para isso); C1, C2 e sequência renderizam com rc=0. Exemplo do contrato em arquivo JSON próprio para os testes de contrato dos dois lados lerem o mesmo arquivo |
| 2026-10-12 | 2026-10-12 21:30 cartão 20 concluído: CaixaDiario.sln com 3 projetos de src e 3 de teste; dotnet build: 0 avisos, 0 erros; docker compose config -q válido (5 serviços, name: caixadiario, healthchecks em bancos e broker); Dockerfiles multi-stage com USER $APP_UID; /health/live e /health/ready |
| 2026-10-12 | 2026-10-12 21:30 cartão 21 concluído: db/lancamentos e db/consolidado aplicados pelo initdb no compose e pela fixture; lancamentos_app com SELECT, INSERT e UPDATE só em outbox.publicado_em; ImutabilidadeTests verde |
| 2026-10-12 | 2026-10-12 21:30 cartão 22 concluído: POST 201, reenvio 200 com Idempotency-Replayed: true, mesma chave com corpo diferente 422, 10 envios simultâneos geram 1 lançamento; GET do dia lista; estorno validado |
| 2026-10-12 | 2026-10-12 21:30 cartão 23 concluído: DespachanteOutbox com espera crescente até 10 s, publicação com confirmação e mandatory; ready só verifica o banco; Rnf01IsolamentoTests: outbox acumula 5 sem broker e esvazia com 5 mensagens na fila |
| 2026-10-12 | 2026-10-12 21:30 cartão 24 concluído: GET consolidado devolve totalCreditos, totalDebitos, saldoDoDia, quantidade, atualizadoAte, defasado; reentrega não soma (ProjetorTests); cache 2 s; limitador token bucket 200/s com fila 50 (rajada: 462 x 200, 1038 x 429) |
| 2026-10-12 | 2026-10-12 21:30 cartão 25 concluído: Rnf01IsolamentoTests verde (Lancamentos + PostgreSQL real, sem Consolidado e sem broker: 201, outbox acumula, broker volta, outbox esvazia); RegrasDeDependenciaTests verde (4 regras NetArchTest) |
| 2026-10-12 | 2026-10-12 21:30 cartão 26 concluído: dotnet test: 46 testes, 0 falhas, 0 avisos (domínio, arquitetura, idempotência, reconciliação ponta a ponta, projetor idempotente, imutabilidade, contrato) |
| 2026-10-12 | 2026-10-12 21:30 cartão 27 concluído: compose up --build do zero; POST 201 e 201; GET saldo 100,50 conferido; stop consolidado consolidado-db; POST 201; volta: saldo 120,50 com 3 lançamentos; nenhum log Error |
| 2026-10-12 | 2026-10-12 21:30 cartão 28 concluído: k6 grafana/k6:1.8.2 na rede do compose: 3001 requisições a 50,01/s por 60 s, http_req_failed 0,00%, p95 1,56 ms |
| 2026-10-12 | Bloco 4: versões novas conferidas no nuget.org e no Docker Hub em 2026-10-12 e validadas pelo restore e pela execução: RabbitMQ.Client 7.2.2, Testcontainers.RabbitMq 4.15.0 (mesma do PostgreSql), rabbitmq:4.1.8-management-alpine, grafana/k6:1.8.2 |
| 2026-10-12 | Bloco 4: despachante e projetor no processo das próprias APIs (`BackgroundService`), sem terceiro executável; senha do papel da aplicação por script `0002_senha_aplicacao.sh` no initdb, fora do SQL versionado |
| 2026-10-12 | Bloco 4: logs JSON pelo provedor nativo (`AddJsonConsole`), sem Serilog: um pacote a menos, mesmo efeito para o escopo |
| 2026-10-12 | Bloco 4: teste de RNF-01 com banco exclusivo; com banco compartilhado, o despachante do teste ponta a ponta esvaziava a outbox contada (achado na segunda execução) |
| 2026-10-12 | Bloco 4: `GSS Encryption Mode=Disable` nas conexões do compose, mesma correção do PacioliBank, para o Npgsql não tentar carregar libgssapi |
