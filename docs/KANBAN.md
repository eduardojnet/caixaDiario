# Quadro Kanban: CaixaDiario

Espelho em texto do quadro mantido no TickTick (projeto `CaixaDiario`), que é a **fonte** das atividades e da ordem. Em divergência, vale o quadro, e este espelho é corrigido. Política de colunas e anatomia do cartão seguem a convenção usada no PacioliBank: título com número de dois dígitos e verbo no infinitivo; âncora, situação, critério verificável e risco; limite de **um** cartão em Em Andamento; Concluído é append-only.

**Data:** 2026-10-12 · **Cartões:** 42 · **Sincronizado com o TickTick em:** 2026-10-12, por leitura direta do quadro

## Distribuição

| Coluna | Cartões | Números |
|---|---|---|
| Backlog/Ideias | 9 | 34 a 42 |
| A Fazer | 29 | 05 a 33 |
| Em Andamento | 1 | 04 |
| Em Revisão | 0 |  |
| Bloqueado | 0 |  |
| Concluído | 3 | 01 a 03 |

## Faixas de numeração

| Faixa | Significado | Bloco do CLAUDE.md |
|---|---|---|
| 01 a 03 | Executado antes da abertura do quadro | Preparação |
| 04 a 04 | Preparação em curso | Preparação |
| 05 a 06 | Pré-voo e repositório | Bloco 0, 10 min |
| 07 a 11 | Fronteira e requisitos | Bloco 1, 65 min |
| 12 a 14 | Decisões e portão G2 | Bloco 2, 40 min |
| 15 a 19 | Arquitetura Alvo | Bloco 3, 35 min |
| 20 a 28 | Código e testes | Bloco 4, 80 min |
| 29 a 32 | README e fechamento | Bloco 5, 40 min |
| 33 a 33 | Revisão final dos obrigatórios | Reserva, 30 min |
| 34 a 42 | Backlog: só entra pelo gatilho declarado | Fora dos 300 min |

**Regra das colunas:** Bloqueado só recebe cartão com o bloqueio nomeado (o 14 vai para lá enquanto aguarda a resposta do G2). Em Revisão recebe trabalho terminado cuja correção ainda não foi medida. Falta de tempo não é bloqueio: é priorização, e vai para Backlog.

---

## Backlog/Ideias

### 34. Desenhar a Arquitetura de Transição a partir de legado

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, arquitetura

ÂNCORA: requisito diferencial 'Arquitetura de Transição'.  
POR QUE NÃO AGORA: cortado na decisão da alternativa B; o enunciado não descreve legado.  
GATILHO: reserva com 20 min ou mais livres após o cartão 33, ou pedido do avaliador.  
ESCOPO: 1. premissa de legado declarada como hipótese; 2. estrangulamento em fases; 3. diagrama de transição.  
CRITÉRIO: docs/05-arquitetura-de-transicao.md com fases e critério de corte de cada fase.

### 35. Estimar o custo de infraestrutura e licenças

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, infra

ÂNCORA: requisito diferencial 'Estimativa de custos'.  
POR QUE NÃO AGORA: cortado na alternativa B; preço de nuvem exige consulta datada.  
GATILHO: reserva com 20 min ou mais livres após o cartão 33.  
ESCOPO: 1. topologia mínima em nuvem; 2. preços consultados e datados [NVI] até conferir; 3. licenças (todas open source previstas).  
CRITÉRIO: tabela de custo mensal com fonte e data de cada preço.

### 36. Implementar observabilidade completa com OpenTelemetry

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, observabilidade

ÂNCORA: requisito diferencial 'Monitoramento e Observabilidade'; ADR-0012 do PacioliBank como referência.  
POR QUE NÃO AGORA: teto de um parágrafo no Bloco 3.  
GATILHO: entrega aceita para próxima etapa, ou pedido do avaliador.  
ESCOPO: 1. traço distribuído pelo evento; 2. métricas de defasagem do projetor e tamanho da outbox; 3. painel local.  
CRITÉRIO: traço único do POST até a projeção visível no painel.

### 37. Implementar a autenticação entre serviços e na API

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, arquitetura

ÂNCORA: requisito diferencial 'Critérios de segurança para integração'.  
POR QUE NÃO AGORA: teto de um parágrafo; ambiente local sem autenticação, declarado.  
GATILHO: entrega aceita para próxima etapa.  
ESCOPO: 1. OAuth2 client credentials na API; 2. credencial de broker por serviço (Lancamentos só publica, Consolidado só consome); 3. TLS no broker.  
CRITÉRIO: teste que prova recusa sem token e recusa de publicação pelo Consolidado.

### 38. Desenhar o C4 nível 3 de componentes

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, diagrama

ÂNCORA: requisito O3, corte de profundidade da alternativa B.  
POR QUE NÃO AGORA: níveis 1 e 2 atendem o requisito; nível 3 é profundidade.  
GATILHO: reserva com 15 min ou mais livres após o cartão 33.  
CRITÉRIO: docs/diagrams/c3-componentes.md desenhado a partir do código, não do desejo.

### 39. Adotar migração versionada com DbUp

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, persistencia

ÂNCORA: trade-off do cartão 21 (scripts de inicialização no lugar de migração).  
POR QUE NÃO AGORA: custo de adaptação maior que o ganho em 5 h.  
GATILHO: primeira mudança de esquema depois da entrega.  
CRITÉRIO: migrador por serviço, subir duas vezes sobre o mesmo volume não reaplica nada.

### 40. Configurar a integração contínua no GitHub Actions

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, teste, infra

ÂNCORA: evidência contínua de build sem aviso e testes verdes; ci.yml do PacioliBank como base.  
POR QUE NÃO AGORA: não é requisito; consome tempo do Bloco 4.  
GATILHO: reserva com 15 min ou mais livres após o cartão 33.  
CRITÉRIO: workflow verde no push, ações fixadas por SHA.

### 41. Aplicar teste de mutação no domínio

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, teste

ÂNCORA: força das asserções do domínio; Stryker já configurado no PacioliBank.  
POR QUE NÃO AGORA: domínio deliberadamente fino neste desafio.  
GATILHO: cartão 40 concluído.  
CRITÉRIO: escore de mutação medido e registrado, com limite no CI.

### 42. Expor o saldo acumulado de fechamento

**Bloco:** Backlog · **Prioridade:** baixa · **Etiquetas:** condicional, dominio

ÂNCORA: QA-01 (saldo do dia ou acumulado).  
POR QUE NÃO AGORA: conduta provisória expõe só o saldo do dia.  
GATILHO: resposta da QA-01 indicando acumulado.  
CRITÉRIO: campo saldoAcumulado no consolidado, com teste de reconciliação entre dias.

---

## A Fazer

### 05. Verificar as ferramentas da máquina local (pré-voo)

**Bloco:** Bloco 0 · **Prioridade:** alta · **Etiquetas:** infra, processo

ÂNCORA: Bloco 0, portão G1, risco de o Bloco 4 falhar por ambiente.  
SITUAÇÃO: não verificado [NVI]: .NET 10, Docker e autenticação git na máquina.  
CRITÉRIO: as cinco ferramentas respondem; saídas registradas no ESTADO §8.  
RISCO: descobrir no Bloco 4 que falta SDK ou Docker consome a reserva inteira.  
PORTÃO G1: faltou ferramenta, parar e decidir (instalar, trocar de máquina, abortar).  
TETO: 5 min (Bloco 0).

- [x] 05.1 dotnet --list-sdks exige 10.x
- [x] 05.2 docker version
- [x] 05.3 docker compose version
- [x] 05.4 git --version
- [x] 05.5 gh auth status ou acesso SSH ao GitHub
- [x] 05.6 Registrar as saídas no ESTADO §8

### 06. Inicializar o repositório local e as convenções de build

**Bloco:** Bloco 0 · **Prioridade:** média · **Etiquetas:** infra, processo

ÂNCORA: Bloco 0, regra 2 (aviso é erro), requisito O8.  
SITUAÇÃO: pasta local não é repositório git; remoto vazio.  
CRITÉRIO: git remote -v aponta para eduardojnet/caixaDiario; commit 'chore: esqueleto do repositorio e convencoes de build' criado (sem push até o G2).  
RISCO: build sem TreatWarningsAsErrors desde o início acumula avisos.  
TETO: 5 min (Bloco 0).

- [x] 06.1 git init -b main
- [x] 06.2 git remote add origin https://github.com/eduardojnet/caixaDiario.git
- [x] 06.3 Copiar Directory.Build.props e global.json do PacioliBank
- [x] 06.4 Copiar docs/adr/template.md do PacioliBank
- [x] 06.5 Commit só dos arquivos da entrega, nunca git add -A

### 07. Mapear a cadeia de valor e as capacidades de negócio

**Bloco:** Bloco 1 · **Prioridade:** alta · **Etiquetas:** documentacao, analise, requisito-obrigatorio

ÂNCORA: requisito O1. Maior buraco do plano: nada equivalente no PacioliBank.  
SITUAÇÃO: inexistente.  
CRITÉRIO: docs/01-capacidades-de-negocio.md com cadeia de valor e mapa de capacidades em dois níveis, cada capacidade com criticidade.  
RISCO: ausência aciona a cláusula de descarte.  
TETO: 15 min (Bloco 1).

- [x] 07.1 Descrever a cadeia de valor do comerciante (vender, receber, pagar, conferir, decidir)
- [x] 07.2 Mapear capacidades nível 1
- [x] 07.3 Detalhar capacidades nível 2
- [x] 07.4 Classificar criticidade de cada capacidade
- [x] 07.5 Ligar capacidades aos dois serviços do enunciado

### 08. Delimitar os domínios funcionais e a linguagem ubíqua

**Bloco:** Bloco 1 · **Prioridade:** alta · **Etiquetas:** documentacao, dominio, requisito-obrigatorio

ÂNCORA: requisito O1, regra 1 (Lancamentos não depende de Consolidado).  
SITUAÇÃO: inexistente.  
CRITÉRIO: docs/02-dominios-funcionais.md com 'Controle de Lançamentos' (núcleo, crítico) e 'Consolidado Diário' (suporte, sacrificável) nominalmente presentes, fronteiras e responsabilidades.  
RISCO: sem a fronteira escrita, a separação em dois serviços fica sem fundamento.  
TETO: 15 min (Bloco 1).

- [x] 08.1 Nomear os domínios e classificá-los (núcleo, suporte)
- [x] 08.2 Definir fronteira e responsabilidade de cada domínio
- [x] 08.3 Registrar a assimetria de criticidade
- [x] 08.4 Glossário: caixa, lançamento, crédito, débito, estorno, data do fato, data do registro, saldo do dia
- [x] 08.5 Declarar a dependência unidirecional entre domínios

### 09. Catalogar os requisitos funcionais

**Bloco:** Bloco 1 · **Prioridade:** média · **Etiquetas:** spec, requisito-obrigatorio

ÂNCORA: requisito O2.  
SITUAÇÃO: inexistente.  
CRITÉRIO: seção RF em docs/03-requisitos.md, RF-xx numerados, cada um rastreado a um domínio.  
RISCO: ausência aciona a cláusula de descarte.  
TETO: 10 min (Bloco 1).

- [x] 09.1 RF de registro de crédito e débito
- [x] 09.2 RF de idempotência da escrita
- [x] 09.3 RF de estorno por lançamento compensatório
- [x] 09.4 RF de listagem dos lançamentos do dia
- [x] 09.5 RF de consulta do saldo diário consolidado
- [x] 09.6 Rastrear cada RF ao domínio

### 10. Catalogar os requisitos não funcionais com métrica, meta e medição

**Bloco:** Bloco 1 · **Prioridade:** alta · **Etiquetas:** spec, requisito-obrigatorio, desempenho

ÂNCORA: requisito O2, RNF literais do enunciado.  
SITUAÇÃO: sementes no CLAUDE.md §7 (modelo, não fato).  
CRITÉRIO: RNF-01 a RNF-07 em docs/03-requisitos.md, cada um com métrica, meta e forma de medição; RNF-01 e RNF-02 presentes com meta e medição.  
RISCO: RNF sem meta não é verificável; é o critério de conclusão do Bloco 1.  
TETO: 15 min (Bloco 1).

- [x] 10.1 RNF-01 isolamento de falha de Lancamentos
- [x] 10.2 RNF-02 50 req/s com perda até 5%
- [x] 10.3 RNF-03 orçamento de erro (SLO 95%)
- [x] 10.4 RNF-04 consistência do saldo
- [x] 10.5 RNF-05 defasagem máxima do consolidado [NVI] até medir
- [x] 10.6 RNF-06 imutabilidade e RNF-07 idempotência

### 11. Registrar as questões abertas com conduta provisória

**Bloco:** Bloco 1 · **Prioridade:** média · **Etiquetas:** analise, requisito-obrigatorio

ÂNCORA: requisito O2 (refinamento), regra 4 (lacuna não vira suposição silenciosa).  
SITUAÇÃO: cinco questões identificadas no CLAUDE.md §7.  
CRITÉRIO: QA-01 a QA-05 em docs/03-requisitos.md, cada uma com conduta provisória e ponto que muda se respondida. Commit 'docs: capacidades, dominios e requisitos'.  
RISCO: suposição silenciosa é o erro que o avaliador procura em refinamento.  
TETO: 10 min (Bloco 1).

- [x] 11.1 QA-01 saldo do dia ou saldo acumulado
- [x] 11.2 QA-02 caixa pode ficar negativo
- [x] 11.3 QA-03 lançamento retroativo
- [x] 11.4 QA-04 um ou vários comerciantes
- [x] 11.5 QA-05 moeda
- [x] 11.6 Verificar critério do Bloco 1 e atualizar ESTADO

### 12. Redigir o ADR-0001 de estilo arquitetural

**Bloco:** Bloco 2 · **Prioridade:** alta · **Etiquetas:** documentacao, arquitetura, requisito-obrigatorio

ÂNCORA: requisito O4, RNF-01, RNF-02.  
SITUAÇÃO: recomendação no CLAUDE.md §6; decisão é de Eduardo no G2.  
CRITÉRIO: ADR em MADR com critérios datados antes das opções; contém literalmente a frase que separa volume de isolamento e nomeia o isolamento de falha como justificativa.  
RISCO: sem a frase, a separação em dois serviços parece superdimensionamento.  
TETO: 20 min (Bloco 2).

- [x] 12.1 Fixar e datar os critérios de decisão
- [x] 12.2 Comparar monolito modular, dois serviços e serverless
- [x] 12.3 Explicitar a conta: 50 req/s, cerca de 4,3 milhões por dia de pico
- [x] 12.4 Escrever a frase volume contra isolamento
- [x] 12.5 Explicar a diferença em relação ao ADR-0001 do PacioliBank
- [x] 12.6 Consequências negativas e gatilho de revisão

### 13. Redigir o ADR-0002 de plataforma, armazenamento e integração

**Bloco:** Bloco 2 · **Prioridade:** alta · **Etiquetas:** documentacao, arquitetura, requisito-obrigatorio

ÂNCORA: requisito O4, L-02 (plano original sem ADR de tecnologia).  
SITUAÇÃO: tabela de transporte A, B, C no CLAUDE.md §6.2. Recomendada: outbox + RabbitMQ.  
CRITÉRIO: cada tecnologia com alternativa rejeitada e gatilho; transporte decidido com trade-off; docs/adr/README.md com índice. Commit 'docs: ADR-0001 e ADR-0002'.  
RISCO: sem ele, o requisito O4 fica atendido pela metade.  
TETO: 15 min (Bloco 2).

- [x] 13.1 .NET 10 LTS e motivo
- [x] 13.2 PostgreSQL 17, um banco por serviço
- [x] 13.3 Dapper no lugar de ORM
- [x] 13.4 Transporte: outbox + broker contra feed HTTP contra banco compartilhado
- [x] 13.5 Testcontainers e k6
- [x] 13.6 Índice docs/adr/README.md

### 14. Obter a aprovação do portão G2 e publicar o primeiro push

**Bloco:** Bloco 2 · **Prioridade:** alta · **Etiquetas:** arquitetura, processo

ÂNCORA: portão G2 (decisão exclusiva de Eduardo), regra 12 (marca de terceiro fora do repositório).  
SITUAÇÃO: aguarda 12 e 13.  
CRITÉRIO: 'G2 aprovado em AAAA-MM-DD HH:MM' no ESTADO §3; origin/main igual ao HEAD local.  
RISCO: push antes da aprovação publica decisão que Eduardo pode reverter.  
COLUNA: vai para Bloqueado enquanto aguarda a resposta, com o bloqueio nomeado.  
TETO: 5 min (Bloco 2).

- [x] 14.1 Apresentar a decisão de cada ADR em uma linha e o trade-off
- [ ] 14.2 Registrar a aprovação no ESTADO §3
- [x] 14.3 Conferir ausência da marca da empresa avaliadora nos arquivos
- [ ] 14.4 git push origin main

### 15. Desenhar o C4 nível 1 de contexto

**Bloco:** Bloco 3 · **Prioridade:** média · **Etiquetas:** diagrama, arquitetura, requisito-obrigatorio

ÂNCORA: requisito O3.  
CRITÉRIO: docs/diagrams/c1-contexto.md em Mermaid, renderiza no GitHub, legenda implementado e especificado, tabela de estado abaixo do desenho.  
RISCO: sem contexto, o C2 perde o leitor.  
TETO: 5 min (Bloco 3).

- [ ] 15.1 Ator: comerciante
- [ ] 15.2 Sistema CaixaDiario
- [ ] 15.3 Externos como especificados (maquininha, banco)
- [ ] 15.4 Criar docs/diagrams/README.md com a convenção de legenda

### 16. Desenhar o C4 nível 2 de contêineres com a dependência unidirecional

**Bloco:** Bloco 3 · **Prioridade:** alta · **Etiquetas:** diagrama, arquitetura, requisito-obrigatorio

ÂNCORA: requisito O3, RNF-01, regra 1.  
CRITÉRIO: docs/diagrams/c2-conteineres.md renderiza sem erro; setas só no sentido Lancamentos → broker → Consolidado; um leitor aponta no desenho por que Lancamentos sobrevive à queda do Consolidado.  
RISCO: é o diagrama que prova o RNF duro; seta de volta invalida a arquitetura.  
TETO: 10 min (Bloco 3).

- [ ] 16.1 Contêineres Lancamentos e banco de lançamentos com outbox
- [ ] 16.2 Broker
- [ ] 16.3 Consolidado e banco do consolidado
- [ ] 16.4 Setas apenas no sentido de Lancamentos para Consolidado
- [ ] 16.5 Validar a sintaxe Mermaid

### 17. Desenhar a sequência de registro, despacho, projeção e consulta

**Bloco:** Bloco 3 · **Prioridade:** média · **Etiquetas:** diagrama, arquitetura

ÂNCORA: requisito O3 (fluxo de dados).  
CRITÉRIO: docs/diagrams/sequencia-registro-e-projecao.md renderiza; mostra lançamento e evento na mesma transação.  
RISCO: sem a sequência, a outbox fica só nomeada.  
TETO: 5 min (Bloco 3).

- [ ] 17.1 POST com Idempotency-Key e transação única
- [ ] 17.2 Despachante publica no broker
- [ ] 17.3 Projetor aplica de forma idempotente
- [ ] 17.4 GET do consolidado com atualizadoAte e defasado

### 18. Especificar o contrato de integração LancamentoRegistrado v1

**Bloco:** Bloco 3 · **Prioridade:** média · **Etiquetas:** arquitetura, spec

ÂNCORA: requisito O3 (integração: protocolo e formato), diferencial de segurança de integração.  
CRITÉRIO: docs/contratos/lancamento-registrado.v1.md com campos, tipos, exemplo JSON e regra de versionamento; sem assembly compartilhado.  
RISCO: contrato implícito acopla os serviços por código.  
TETO: 5 min (Bloco 3).

- [ ] 18.1 Campos: eventoId, versao, lancamentoId, caixaId, tipo, valor, dataDoFato, registradoEm
- [ ] 18.2 Exemplo JSON versionado
- [ ] 18.3 Regra de evolução de versão
- [ ] 18.4 Semântica ao menos uma vez e idempotência do consumidor

### 19. Redigir o documento de Arquitetura Alvo

**Bloco:** Bloco 3 · **Prioridade:** alta · **Etiquetas:** documentacao, arquitetura, observabilidade, requisito-obrigatorio

ÂNCORA: requisito O3, diferenciais de observabilidade e segurança (um parágrafo cada, teto).  
CRITÉRIO: docs/04-arquitetura-alvo.md com visão, decisões aplicadas, degradação, observabilidade e segurança. Commit e push 'docs: arquitetura alvo, C4 e contrato de integracao'.  
RISCO: abrir os diferenciais além de um parágrafo consome o tempo do Bloco 4.  
TETO: 10 min (Bloco 3).

- [ ] 19.1 Visão e ligação com os ADRs
- [ ] 19.2 Estratégia de degradação: cache, limitador, 429 no orçamento, defasado
- [ ] 19.3 Um parágrafo de observabilidade
- [ ] 19.4 Um parágrafo de segurança de integração
- [ ] 19.5 Verificar critério do Bloco 3, atualizar ESTADO e push

### 20. Montar a solução .NET e o docker compose

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** codigo, infra

ÂNCORA: requisitos O5 e O7, RNF-01 (um banco por serviço).  
CRITÉRIO: dotnet build sem avisos; docker compose config válido com 2 APIs, 2 bancos e broker, nome de projeto fixo e healthchecks.  
RISCO: é o bloco que estoura primeiro; falha aqui, sozinha, aciona o descarte.  
TETO: 12 min (Bloco 4).

- [ ] 20.1 CaixaDiario.sln com Lancamentos.Dominio, Lancamentos.Api, Consolidado.Api
- [ ] 20.2 Projetos de teste: Dominio, Arquitetura, Integracao
- [ ] 20.3 Dockerfile multi-stage com USER $APP_UID para cada API
- [ ] 20.4 docker-compose.yml: lancamentos, lancamentos-db, broker, consolidado, consolidado-db
- [ ] 20.5 .env.example
- [ ] 20.6 Health checks /health/live e /health/ready

### 21. Implementar o esquema de banco dos dois serviços

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** codigo, persistencia

ÂNCORA: RNF-06, RNF-07, regra 7 (invariante por constraint e privilégio).  
CRITÉRIO: db/lancamentos/0001_esquema.sql e db/consolidado/0001_esquema.sql aplicados no compose e na fixture; papel da aplicação com SELECT e INSERT apenas no ledger.  
RISCO: imutabilidade só em código não é controle.  
TRADE-OFF: sem DbUp neste prazo (cartão 39 no backlog).  
TETO: 8 min (Bloco 4).

- [ ] 21.1 Ledger append-only com chave única de idempotência
- [ ] 21.2 Tabela outbox
- [ ] 21.3 Papéis e GRANT SELECT, INSERT
- [ ] 21.4 saldo_diario e lancamentos_aplicados no consolidado
- [ ] 21.5 Montar via docker-entrypoint-initdb.d

### 22. Implementar o registro de lançamento idempotente em Lancamentos

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** codigo, dominio

ÂNCORA: RF de registro, RNF-07, ADR-0006 do PacioliBank (idempotência por violação de chave única).  
CRITÉRIO: POST /v1/caixas/{caixaId}/lancamentos responde 201 novo, 200 com Idempotency-Replayed no reenvio, 422 com mesma chave e corpo diferente; GET do dia lista.  
RISCO: reenvio duplicado corrompe o saldo.  
TETO: 12 min (Bloco 4).

- [ ] 22.1 Domínio: Lancamento, TipoLancamento, valor positivo, data futura recusada
- [ ] 22.2 Endpoint POST com Idempotency-Key obrigatório
- [ ] 22.3 Lançamento e evento na mesma transação
- [ ] 22.4 Detecção de reenvio por violação de chave única
- [ ] 22.5 GET /v1/caixas/{caixaId}/lancamentos?data=

### 23. Implementar a outbox e o despachante para o broker

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** codigo, arquitetura

ÂNCORA: ADR-0002 (transporte aprovado no G2), RNF-01, ADR-0008 do PacioliBank.  
CRITÉRIO: com broker no ar, a outbox esvazia; com broker fora, o POST segue respondendo 201 e a outbox acumula.  
RISCO: despachante bloqueante transforma queda do broker em queda de Lancamentos.  
TETO: 10 min (Bloco 4).

- [ ] 23.1 BackgroundService de despacho com backoff
- [ ] 23.2 Publicador real no broker
- [ ] 23.3 Marca de publicado e entrega ao menos uma vez
- [ ] 23.4 Ready de Lancamentos não depende do broker

### 24. Implementar o projetor e a consulta do Consolidado

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** codigo, desempenho

ÂNCORA: RF de consulta, RNF-02, RNF-03, RNF-04, RNF-05.  
CRITÉRIO: GET /v1/caixas/{caixaId}/consolidado/{data} devolve totalCreditos, totalDebitos, saldoDoDia, quantidade, atualizadoAte, defasado; evento repetido não soma duas vezes.  
RISCO: projetor não idempotente quebra a reconciliação.  
TETO: 12 min (Bloco 4).

- [ ] 24.1 Consumidor do evento LancamentoRegistrado v1 com DTO próprio
- [ ] 24.2 Aplicação idempotente por lancamentoId
- [ ] 24.3 Endpoint de consulta do consolidado
- [ ] 24.4 Cache em memória de curta duração
- [ ] 24.5 Limitador de taxa com fila curta, excedente em 429
- [ ] 24.6 Marca de defasagem

### 25. Provar o RNF-01: Lancamentos aceita escrita sem Consolidado nem broker

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** teste, risco, requisito-obrigatorio

ÂNCORA: RNF-01, regra 1. O teste mais importante da entrega.  
CRITÉRIO: teste de integração verde: Lancamentos com PostgreSQL real (Testcontainers), sem Consolidado e sem broker, responde 201; outbox acumula; com o broker de volta, a outbox esvazia. Teste de arquitetura verde: nenhum tipo CaixaDiario.Lancamentos.* referencia CaixaDiario.Consolidado.*.  
RISCO: sem prova executável, o único RNF duro do enunciado fica só declarado.  
TETO: 8 min (Bloco 4).

- [ ] 25.1 Fixture Testcontainers de PostgreSQL e broker
- [ ] 25.2 Teste: 201 sem Consolidado e sem broker
- [ ] 25.3 Teste: outbox acumula e depois esvazia
- [ ] 25.4 Teste NetArchTest da dependência unidirecional

### 26. Testar idempotência, reconciliação, imutabilidade e domínio

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** teste, requisito-obrigatorio

ÂNCORA: requisito O6, RNF-04, RNF-06, RNF-07, regra 8 (banco real).  
CRITÉRIO: dotnet test verde, sem aviso, com os casos abaixo.  
REGRA DE CORTE: se o tempo apertar, corta-se do fim da lista, nunca o 25.  
TETO: 8 min (Bloco 4).

- [ ] 26.1 Idempotência: mesma chave e corpo, um lançamento; corpo diferente, 422
- [ ] 26.2 Reconciliação: saldo projetado igual a créditos menos débitos do dia
- [ ] 26.3 Projetor idempotente: evento duplicado não soma duas vezes
- [ ] 26.4 Imutabilidade: UPDATE e DELETE recusados pelo banco
- [ ] 26.5 Domínio: valor positivo, tipo válido, data futura recusada

### 27. Verificar o ambiente completo pelo docker compose

**Bloco:** Bloco 4 · **Prioridade:** alta · **Etiquetas:** teste, infra, requisito-obrigatorio

ÂNCORA: requisito O7, critério de conclusão do Bloco 4.  
CRITÉRIO: docker compose up --build -d sobe tudo; POST responde 201; GET do consolidado devolve o saldo esperado; docker compose stop consolidado consolidado-db seguido de POST ainda responde 201. Commits por decisão e push.  
RISCO: se não subir na máquina do avaliador, documentação perfeita não salva.  
TETO: 5 min (Bloco 4).

- [ ] 27.1 Subir o ambiente do zero
- [ ] 27.2 Registrar lançamento e conferir o saldo
- [ ] 27.3 Derrubar Consolidado e banco dele e lançar de novo
- [ ] 27.4 Registrar as saídas no ESTADO e fazer push

### 28. Medir o RNF-02 com teste de carga a 50 req/s

**Bloco:** Bloco 4 · **Prioridade:** média · **Etiquetas:** condicional, teste, desempenho

ÂNCORA: RNF-02, RNF-03.  
CRITÉRIO: tests/carga/consolidado-50rps.js versionado; executado via contêiner k6 na rede do compose com taxa constante de 50/s por 60 s e http_req_failed < 0.05.  
CONDICIONAL: se o teto do Bloco 4 chegar antes, o script fica versionado e marcado 'especificado, não executado' no ESTADO §5.  
RISCO: sem medição, a meta de 5% fica declarada sem evidência.  
TETO: 5 min (Bloco 4).

- [ ] 28.1 Escrever o script k6 com cenário constant-arrival-rate
- [ ] 28.2 Fixar a imagem do k6 em versão conferida [NVI]
- [ ] 28.3 Executar e registrar taxa de erro e p95

### 29. Redigir a seção do README: como a aplicação funciona

**Bloco:** Bloco 5 · **Prioridade:** alta · **Etiquetas:** documentacao, requisito-obrigatorio

ÂNCORA: requisito O7 (primeira metade).  
CRITÉRIO: seção própria e nomeada, separada de 'como rodar'.  
RISCO: o enunciado pede as duas coisas; uma só atende metade do item.  
TETO: 8 min (Bloco 5).

- [ ] 29.1 Problema do comerciante em três linhas
- [ ] 29.2 Os dois serviços e por que dois (isolamento, não volume)
- [ ] 29.3 Fluxo de um lançamento até o consolidado
- [ ] 29.4 Link para o C2 e para os ADRs

### 30. Redigir a seção do README: como rodar localmente

**Bloco:** Bloco 5 · **Prioridade:** alta · **Etiquetas:** documentacao, requisito-obrigatorio

ÂNCORA: requisito O7 (segunda metade).  
CRITÉRIO: passos numerados, cada um com o resultado esperado; seções 'Se algo der errado' e 'Parar e começar do zero'; demonstração do RNF-01 em três comandos.  
RISCO: passo sem resultado esperado vira pergunta do avaliador.  
TETO: 12 min (Bloco 5).

- [ ] 30.1 Pré-requisitos com versão
- [ ] 30.2 Passos numerados com resultado esperado
- [ ] 30.3 Exemplos de requisição
- [ ] 30.4 Demonstração do RNF-01 em três comandos
- [ ] 30.5 Se algo der errado; parar e começar do zero
- [ ] 30.6 Como rodar os testes

### 31. Completar o README com índice, estado, citação e evoluções

**Bloco:** Bloco 5 · **Prioridade:** média · **Etiquetas:** documentacao, requisito-obrigatorio

ÂNCORA: requisito O9, L-01 (números da citação), observação do enunciado sobre evoluções futuras.  
CRITÉRIO: tabela 'Estado atual' coerente com ESTADO §4 e §5; citação do PacioliBank com números reconferidos no dia; lista 'O que faria com mais tempo' igual aos cartões 34 a 42.  
RISCO: número desatualizado na citação é desmentido em um clique.  
TETO: 8 min (Bloco 5).

- [ ] 31.1 Índice da documentação
- [ ] 31.2 Tabela Estado atual, implementado e especificado
- [ ] 31.3 Reconferir os números do PacioliBank e inserir a citação
- [ ] 31.4 O que faria com mais tempo
- [ ] 31.5 Licença MIT

### 32. Verificar a entrega por clone limpo e publicar o push final

**Bloco:** Bloco 5 · **Prioridade:** alta · **Etiquetas:** processo, teste, requisito-obrigatorio

ÂNCORA: requisitos O7 e O8, critério do Bloco 5.  
CRITÉRIO: em pasta temporária, git clone do repositório público e execução só pelo README, sem consultar mais nada; origin/main igual ao HEAD local.  
RISCO: arquivo que só existe na pasta local passa despercebido.  
TETO: 7 min (Bloco 5).

- [ ] 32.1 Clonar em pasta temporária
- [ ] 32.2 Seguir só o README até o saldo conferido
- [ ] 32.3 Corrigir o README se algum passo exigiu conhecimento externo
- [ ] 32.4 Push final e conferência de origin/main

### 33. Revisar os nove requisitos obrigatórios contra o repositório publicado

**Bloco:** Revisão final · **Prioridade:** alta · **Etiquetas:** processo, risco, requisito-obrigatorio

ÂNCORA: cláusula de descarte, ESTADO §2.  
CRITÉRIO: tabela O1 a O9 no ESTADO §2 sem nenhum 'Pendente', cada linha com link para o arquivo no GitHub; nenhuma menção à marca da empresa avaliadora.  
RISCO: um item ausente descarta o teste inteiro.  
TETO: 5 min, mais a reserva de 30 min se disponível.

- [ ] 33.1 Conferir O1 a O9 no repositório publicado
- [ ] 33.2 Buscar a marca da empresa avaliadora em todos os arquivos
- [ ] 33.3 Conferir ESTADO §7 com tempos reais
- [ ] 33.4 Atualizar o espelho docs/KANBAN.md

---

## Em Andamento

### 04. Instalar as instruções locais do Claude Code

**Bloco:** Preparação · **Prioridade:** alta · **Etiquetas:** infra, processo

ÂNCORA: cartão 03. A ponte remota não grava em .claude/, por isso os arquivos ficaram em _instalar/.  
SITUAÇÃO: pastas .claude/skills/proximo criadas; primeiro mv falhou sem destino, nada perdido.  
CRITÉRIO: ls -R .claude mostra settings.json e skills/proximo/SKILL.md; /status sem Settings Error; /proximo aparece na lista de comandos.  
RISCO: sem isso, nenhum bloco roda pelo procedimento único.  
TETO: fora dos 300 min (preparação).

- [ ] 04.1 Copiar _instalar/settings.json para .claude/
- [ ] 04.2 Copiar _instalar/SKILL.md para .claude/skills/proximo/
- [ ] 04.3 Remover a pasta _instalar
- [ ] 04.4 Abrir o Claude Code e conferir /status
- [ ] 04.5 Confirmar /proximo na lista de comandos

---

## Em Revisão

_Vazia._

---

## Bloqueado

_Vazia._

---

## Concluído

### 01. Analisar o enunciado e definir a estratégia de entrega

**Bloco:** Preparação · **Prioridade:** média · **Etiquetas:** analise, processo

ÂNCORA: enunciado (PDF), requisitos O1 a O9, RNF-01 e RNF-02.  
SITUAÇÃO: concluído antes da abertura do quadro.  
CRITÉRIO: documento de contexto existe no projeto do claude.ai, com decisão e conta de tempo.  
ENTREGA: documento de contexto (fora do repositório). Alternativa B (reaproveitar oficina e método do PacioliBank, peça nova para o domínio do comerciante). A e C rejeitadas com motivo.

- [x] 01.1 Extrair os requisitos obrigatórios, diferenciais e RNF literais
- [x] 01.2 Comparar alternativas A, B e C pela conta de tempo
- [x] 01.3 Definir cortes de profundidade sem remover item obrigatório
- [x] 01.4 Registrar a fila de 5 blocos com teto

### 02. Verificar os acessos antes de iniciar

**Bloco:** Preparação · **Prioridade:** média · **Etiquetas:** infra, processo

ÂNCORA: requisito O8 (repositório público).  
CRITÉRIO: cada acesso testado com operação real, não presumido.  
ENTREGA (2026-10-12): GitHub eduardojnet/caixaDiario público, vazio, permissão admin e push (push em dry-run). PacioliBank legível. Pasta local com leitura e escrita (gravado .gitignore). Shell remoto no computador indisponível: não bloqueia, o Claude Code local executa.

- [x] 02.1 Ler o contexto e o PDF do projeto
- [x] 02.2 Testar escrita no repositório GitHub
- [x] 02.3 Testar leitura do PacioliBank
- [x] 02.4 Testar leitura e escrita na pasta local

### 03. Gerar as instruções de execução do Claude Code

**Bloco:** Preparação · **Prioridade:** média · **Etiquetas:** documentacao, processo

ÂNCORA: L-01 (citação com números falsos), L-02 (ADR de tecnologia ausente), realocação de 55 min.  
CRITÉRIO: arquivos gravados e listados na pasta local.  
ENTREGA: CLAUDE.md (persona Arquiteto de Soluções Sênior, 16 regras, fila, portões G1 a G3), skill /proximo, settings.json, docs/ESTADO.md, .gitignore, .editorconfig. Cópia do CLAUDE.md no projeto do claude.ai.

- [x] 03.1 Extrair a oficina real do PacioliBank (build, testes, compose, versões)
- [x] 03.2 Redigir CLAUDE.md com persona, regras e fila
- [x] 03.3 Redigir a skill /proximo, procedimento único
- [x] 03.4 Redigir settings.json com permissões e negações
- [x] 03.5 Registrar o estado inicial em docs/ESTADO.md
- [x] 03.6 Corrigir a citação do PacioliBank (94,11% e 86,96%)

