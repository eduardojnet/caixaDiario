# ADR-0001: Separar registro e consolidado em dois serviços com integração assíncrona

- **Status:** Aceito em 2026-10-12 21:06 (portão G2)
- **Data:** 2026-10-12
- **Decisor:** Eduardo J. G. do Carmo (Arquiteto de Soluções)
- **Consultados:** nenhum além do enunciado
- **Requisitos dirigentes:** RNF-01, RNF-02, RNF-03, RNF-04, RE-01, RE-05, PR-03

## Contexto e problema

O enunciado pede dois serviços: controle de lançamentos e consolidado diário. Pede também, por escrito, que o primeiro não fique indisponível se o segundo cair (RNF-01), e que o segundo suporte 50 requisições por segundo em pico com até 5% de perda (RNF-02).

Os dois domínios têm criticidade assimétrica ([02-dominios-funcionais.md](../02-dominios-funcionais.md) §3): o registro é o fato financeiro e não pode parar; o consolidado é uma visão derivada, reconstruível e tolerante a perda.

A pergunta é: qual estilo de implantação garante que nenhuma falha do consolidado alcance o registro, com o menor custo de operação compatível com a carga declarada?

**A conta da carga.** 50 req/s sustentadas por 24 horas são 50 × 86.400 ≈ 4,3 milhões de requisições num dia de pico. É um teto pessimista: o pico real dura algumas horas. Uma consulta ao consolidado é uma leitura por chave primária num modelo de leitura pequeno. Um único processo ASP.NET Core com PostgreSQL em hardware modesto absorve essa ordem de grandeza com folga `[NVI]`: a ser validado pelo teste de carga do RNF-02. **A carga não é a restrição dura.**

## Critérios de decisão

Fixados em 2026-10-12, antes da comparação das opções, em ordem de peso:

1. **Isolamento de falha:** nenhum modo de falha do consolidado (processo fora, banco fora, sobrecarga, defeito, vazamento de memória, implantação ruim) pode tornar o registro indisponível. RNF-01, RE-01.
2. **Integridade do fato financeiro:** o registro de um lançamento e seu anúncio ao consolidado não podem divergir. Sem lançamento sem anúncio e sem anúncio sem lançamento. RNF-04.
3. **Proporcionalidade:** o custo de operação acompanha a carga declarada, não uma carga imaginada.
4. **Demonstrabilidade:** sobe localmente com um comando e o critério 1 é provado por teste automatizado no prazo do desafio. RE-04, RE-05.
5. **Caminho de evolução:** escalar o consolidado sem tocar no registro, se a carga crescer.

## Opções consideradas

1. **Monolito modular:** um processo, dois módulos com fronteira verificada por teste, um banco.
2. **Dois serviços com integração assíncrona:** dois processos implantáveis de forma independente, um banco por serviço, o registro anuncia lançamentos por evento e o consolidado projeta um modelo de leitura.
3. **Serverless:** uma função por operação, com fila gerenciada entre registro e consolidado.

## Decisão

**A solução separa o Controle de Lançamentos e o Consolidado Diário em dois serviços implantáveis de forma independente, cada um com seu banco, integrados de forma assíncrona por eventos, com dependência só no sentido Lançamentos → Consolidado.**

> **O volume não justifica a separação; o isolamento de falha justifica.** 50 req/s, cerca de 4,3 milhões de requisições num dia de pico, cabem num único processo. O que um único processo não entrega é a garantia de que a falha do consolidado não alcança o registro.

**Justificativa pelos critérios:**

- **Critério 1, decisivo.** Num monolito modular, os dois módulos compartilham processo, memória, pool de threads, pool de conexões e ciclo de implantação. Uma consulta de consolidado que esgota memória ou conexões derruba o registro junto. Disjuntores e anteparas reduzem esse risco, mas o reduzem por disciplina de código, não por estrutura. Com dois processos e dois bancos, a falha do consolidado não tem caminho físico até o registro.
- **Critério 2.** O registro grava o lançamento e o evento na mesma transação local, no próprio banco (outbox transacional, [ADR-0002](ADR-0002-plataforma-armazenamento-e-integracao.md)). Não há transação distribuída, porque nenhuma invariante do registro depende do estado do consolidado.
- **Critério 3.** Cada serviço roda como instância única e pequena. Sem orquestrador, sem malha de serviço, sem cluster.
- **Critério 4.** Docker Compose sobe os dois serviços, os dois bancos e o broker. O isolamento é provado parando o consolidado e o banco dele e registrando um lançamento.
- **Critério 5.** O consolidado escala horizontalmente sozinho, porque só lê o próprio modelo.

### Por que a conclusão difere do ADR-0001 do PacioliBank

O autor escolheu monolito modular em outro projeto ([PacioliBank, ADR-0001](https://github.com/eduardojnet/PacioliBank/blob/main/docs/adr/ADR-0001-estilo-arquitetural.md)), com o mesmo método. A conclusão muda porque a premissa muda:

| | PacioliBank | CaixaDiario |
|---|---|---|
| Critério decisivo | Consistência forte por conta: posição nunca negativa, verificada na mesma transação do lançamento | Isolamento de falha entre registro e consolidado, exigido por escrito |
| Invariante que atravessa os domínios | Sim: a posição consolidada participa da validação do lançamento | Não: o registro nunca consulta o saldo (QA-02, caixa pode ficar negativo) |
| Efeito de separar | Saga com estado intermediário visível no lugar de transação ACID | Consistência eventual numa visão derivada, sinalizada ao usuário |
| Requisito de isolamento entre capacidades | Ausente | RNF-01 |

Mesmo método, premissa diferente, conclusão diferente. Se QA-02 for respondida com "o caixa não pode ficar negativo", a posição volta a participar da validação do lançamento, e o critério 2 passa a favorecer a opção 1 (ver gatilho de revisão).

## Consequências

**Positivas**

- O RNF-01 é garantido por estrutura: processo, banco e implantação separados.
- O consolidado pode degradar (limitar, recusar com `429`, servir cache, ficar defasado) sem qualquer efeito sobre o registro.
- Cada serviço escala e é implantado no seu ritmo.

**Negativas**

- **Consistência eventual:** o saldo consultado pode não refletir o lançamento de segundos atrás. Mitigação: `atualizadoAte` e `defasado` na resposta (RNF-05, RF-09).
- **Mais peças a operar:** dois processos, dois bancos e um broker, contra um processo e um banco.
- **Entrega ao menos uma vez:** o consolidado precisa aplicar cada evento de forma idempotente (RF-10).
- **Contrato entre serviços:** mudança no evento exige versionamento e coordenação; refatoração entre os dois deixa de ser verificada pelo compilador.
- **Depuração distribuída:** um lançamento atravessa dois processos; rastrear exige correlação pelo identificador do evento.

**Neutras**

- Um único repositório com os dois serviços, sem assembly compartilhado.

### Impacto técnico

| Área | Impacto |
|---|---|
| Aplicações | Duas APIs ASP.NET Core; o consolidado hospeda também o projetor |
| Integrações | Um evento versionado, `LancamentoRegistrado` v1, por broker |
| Infraestrutura | Dois PostgreSQL e um broker, uma instância de cada |
| Dados | Registro append-only em Lançamentos; modelo de leitura reconstruível em Consolidado |
| Segurança | Credencial por serviço com privilégio mínimo (detalhe na Arquitetura Alvo) |
| Desenvolvimento | Teste de arquitetura impede referência de Lançamentos a Consolidado |
| Operação | Monitorar tamanho da outbox e defasagem do projetor |

### Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Dependência acidental de Lançamentos a Consolidado (código, configuração, verificação de saúde) | Teste de arquitetura (NetArchTest); prontidão de Lançamentos verifica só o próprio banco; teste de integração sem Consolidado |
| Broker fora impede o anúncio | Outbox retém os eventos no banco do registro; o despacho retoma quando o broker volta |
| Evento duplicado soma duas vezes | Chave única por lançamento no consolidado |
| Projeção divergir do registro | Teste de reconciliação (RNF-04); modelo de leitura reconstruível a partir dos eventos |

### Evolução

Sem reescrita: o consolidado ganha réplicas atrás de um balanceador quando a carga medida exigir; o modelo de leitura pode ir para armazenamento chave-valor se a consulta por chave virar gargalo medido. Nenhuma dessas mudanças toca o registro.

## Análise das opções rejeitadas

**Monolito modular.** Atende os critérios 2, 3 e 4 melhor que a opção escolhida: uma transação, um processo, menos peças. Falha no critério 1, que tem o maior peso: o isolamento entre módulos no mesmo processo é lógico, não físico. *Voltaria a ser a melhor escolha* se o requisito de isolamento entre as capacidades saísse do enunciado, ou se uma invariante do registro passasse a depender do saldo consolidado (por exemplo, QA-02 respondida com "caixa não pode ficar negativo").

**Serverless.** Atende o critério 1 e escala por demanda. Falha no critério 4: o ambiente local depende de emuladores e não reproduz a plataforma. Partida a frio introduz latência variável no registro, e conexões a PostgreSQL a partir de funções exigem um intermediário de pool. *Voltaria a ser considerado* se a carga se tornasse esporádica com longos períodos ociosos, e o custo por hora de instância fosse o fator dominante.

## Validação

- Teste de arquitetura: nenhum tipo `CaixaDiario.Lancamentos.*` referencia `CaixaDiario.Consolidado.*`.
- Teste de integração: Lançamentos com banco real, sem Consolidado e sem broker, responde `201`.
- Demonstração: `docker compose stop consolidado consolidado-db` seguido de registro responde `201`.

## Gatilho de revisão

- QA-02 respondida com "o caixa não pode ficar negativo", ou qualquer outra regra que faça o registro depender do saldo.
- Requisito de consistência imediata do saldo após o registro (contradiz PR-03).
- Retirada do requisito de isolamento entre as duas capacidades.
