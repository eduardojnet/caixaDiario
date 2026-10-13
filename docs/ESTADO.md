# Estado do Projeto

**Documento vivo.** Atualizado ao fim de cada bloco de trabalho. Descreve o que existe, o que falta e o que está decidido, sem otimismo.

**Última atualização:** 2026-10-12 (Bloco 2 concluído, G2 aprovado)
**Build:** convenções definidas (`Directory.Build.props`, `global.json`); nenhum projeto ainda
**Testes:** inexistentes

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
| O3 | Desenho da solução completo (Arquitetura Alvo) | Pendente (Bloco 3) |
| O4 | Justificativa de ferramentas, tecnologias e tipo de arquitetura | Atendido: `docs/adr/ADR-0001`, `docs/adr/ADR-0002`, aceitos no G2 |
| O5 | Linguagem dominada | Previsto: C# / .NET 10 |
| O6 | Testes | Pendente (Bloco 4) |
| O7 | README: como funciona e como rodar localmente | Pendente (Bloco 5) |
| O8 | Repositório público no GitHub | Publicado: https://github.com/eduardojnet/caixaDiario, push a cada bloco desde o G2 |
| O9 | Toda documentação no repositório | Em andamento: Bloco 1 em `docs/` |

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

- Esqueleto do repositório: `git init`, remoto `origin`, `Directory.Build.props` (.NET 10, `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, `NuGetAudit`), `global.json` (Microsoft Testing Platform), `docs/adr/template.md` (MADR em português), `.editorconfig`, `.gitignore`.

---

## 5. Especificado, não implementado

- RF-01 a RF-10 e RNF-01 a RNF-07: especificados em `docs/03-requisitos.md`; implementação e medição previstas no Bloco 4.
- RNF-05: meta proposta de 5 s `[NVI]` até medir.

---

## 6. Questões abertas e lacunas

| ID | Questão | Conduta provisória | Origem |
|---|---|---|---|
| L-01 | Números do PacioliBank no texto de citação divergiam do repositório (95% e 87% declarados; 94,11% e 86,96% medidos) | Texto corrigido no CLAUDE.md §5.3; reconferir no Bloco 5 | Verificação de 2026-10-12 |
| L-02 | Plano original não tinha ADR de tecnologia (requisito O4) | ADR-0002 incluído no Bloco 2 | Verificação de 2026-10-12 |
| QA-01 a QA-05 | Questões de negócio do enunciado | Condutas provisórias em `docs/03-requisitos.md` §7 | Bloco 1 |
| PR-01 a PR-04 | Premissas com impacto declarado | `docs/03-requisitos.md` §5 | Bloco 1 |

---

## 7. Fila de execução

| Bloco | Teto (min) | Início | Fim | Gasto | Estado | Critério de conclusão |
|---|---|---|---|---|---|---|
| 0. Pré-voo e repositório | 10 | 19:55 | 19:57 | 2 | Concluído | Ferramentas respondem; remoto configurado |
| 1. Fronteira e requisitos | 65 | 21:00 | 21:03 | 3 | Concluído | Dois serviços nominais no mapa; RNF-01 e RNF-02 com meta e medição |
| 2. Decisões | 40 | 21:04 | 21:06 | 2 | Concluído | Frase volume x isolamento no ADR-0001; G2 aprovado |
| 3. Arquitetura Alvo | 35 | | | | Pendente | C2 renderiza; sobrevivência de Lancamentos visível |
| 4. Código e testes | 80 | | | | Pendente | Compose sobe; POST 201; saldo confere; testes passam; POST 201 com Consolidado parado |
| 5. README e fechamento | 40 | | | | Pendente | Clone limpo sobe só com o README |
| Reserva | 30 | | | | Disponível | Consumida apenas pelo Bloco 4 ou por revisão final |
| **Total** | **300** | | | **7** | | |

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
