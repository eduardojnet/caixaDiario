# Requisitos

**Público:** TI. **Nível:** arquitetura, governança e riscos.
**Requisito atendido:** refinamento de requisitos funcionais e não funcionais (O2).
**Data:** 2026-10-12.

Este documento separa **requisito** (o que a solução deve fazer ou garantir), **restrição** (o que limita a escolha), **premissa** (o que se assume verdadeiro, com impacto se falhar), **driver arquitetural** (o que mais pesa no desenho) e **questão aberta** (lacuna de negócio com conduta provisória).

## 1. Requisitos do enunciado, literais

| Origem | Texto |
|---|---|
| Funcional | Serviço que faça o controle de lançamentos (débitos e créditos) do fluxo de caixa diário |
| Funcional | Serviço do consolidado diário: relatório com o saldo diário consolidado |
| Não funcional | "O serviço de controle de lançamento não deve ficar indisponível se o sistema de consolidado diário cair." |
| Não funcional | "Em dias de picos, o serviço de consolidado diário recebe 50 requisições por segundo, com no máximo 5% de perda de requisições." |

## 2. Requisitos funcionais

| ID | Requisito | Domínio | Capacidade |
|---|---|---|---|
| RF-01 | Registrar lançamento de **crédito** em um caixa, com valor positivo, data do fato e descrição | Controle de Lançamentos | C1.1 |
| RF-02 | Registrar lançamento de **débito** em um caixa, com valor positivo, data do fato e descrição | Controle de Lançamentos | C1.2 |
| RF-03 | Recusar lançamento inválido: valor zero ou negativo, tipo diferente de crédito e débito, data do fato futura | Controle de Lançamentos | C1.1, C1.2 |
| RF-04 | Exigir chave de idempotência em todo registro. Reenvio com a mesma chave e o mesmo conteúdo devolve o lançamento original sem criar outro; mesma chave com conteúdo diferente é recusada | Controle de Lançamentos | C1.3 |
| RF-05 | Corrigir lançamento errado **somente por estorno**: novo lançamento de tipo oposto e mesmo valor, referenciando o original. Não existe alteração nem exclusão | Controle de Lançamentos | C1.4 |
| RF-06 | Listar os lançamentos de um caixa em uma data do fato | Controle de Lançamentos | C2.1 |
| RF-07 | Anunciar cada lançamento registrado em formato versionado, sem conhecer quem consome | Controle de Lançamentos | C2.2 (habilita) |
| RF-08 | Consultar o consolidado de um caixa em uma data: total de créditos, total de débitos, saldo do dia, saldo acumulado até a data e quantidade de lançamentos | Consolidado Diário | C2.2 |
| RF-09 | Informar na consulta até quando o consolidado está atualizado e se está defasado | Consolidado Diário | C2.3 |
| RF-10 | Aplicar cada lançamento anunciado ao consolidado uma única vez, mesmo com anúncio repetido | Consolidado Diário | C2.2 |

## 3. Requisitos não funcionais

Cada RNF tem **métrica** (o que se mede), **meta** (o valor aceito) e **medição** (como se verifica). Estado da medição em [ESTADO.md](ESTADO.md).

| ID | Requisito | Métrica | Meta | Medição |
|---|---|---|---|---|
| **RNF-01** | Disponibilidade do Controle de Lançamentos independe do Consolidado Diário (enunciado) | Proporção de registros válidos aceitos com o Consolidado, o banco dele e o canal de anúncio fora do ar | 100% | Teste de integração automatizado com Lançamentos e banco real, sem Consolidado e sem broker; demonstração com `docker compose stop` do Consolidado e do banco dele seguida de registro; teste de arquitetura que proíbe referência de Lançamentos a Consolidado |
| **RNF-02** | Capacidade do Consolidado em pico (enunciado) | Taxa de requisições falhas sob carga constante | 50 req/s sustentadas com no máximo 5% de falha | Teste de carga com taxa de chegada constante de 50 req/s por 60 s, limite `http_req_failed < 0.05` |
| **RNF-03** | Orçamento de erro do Consolidado | Respostas não 2xx sobre o total, em janela de pico | SLO de 95% de respostas 2xx; recusa por limitação (`429`) conta como falha e consome o orçamento | Mesmo teste do RNF-02; em operação, métrica de respostas por código |
| **RNF-04** | Consistência do saldo | Diferença entre o saldo projetado e Σ créditos − Σ débitos do dia, recalculado do registro | Zero, depois que a projeção alcança o último lançamento | Teste de reconciliação automatizado |
| **RNF-05** | Defasagem máxima do consolidado em operação normal | Tempo entre o registro do lançamento e sua presença no consolidado | Proposta: até 5 s `[NVI]` até medir. Acima do limite, a resposta marca `defasado: true` | `atualizadoAte` comparado ao relógio; teste de integração do fluxo completo |
| **RNF-06** | Imutabilidade do lançamento | Caminhos de alteração ou exclusão disponíveis à aplicação | Nenhum | Privilégio de banco: o papel da aplicação tem só `SELECT` e `INSERT` no registro de lançamentos; teste contra PostgreSQL real que tenta `UPDATE` e `DELETE` e espera recusa |
| **RNF-07** | Idempotência do registro | Lançamentos criados por N envios com a mesma chave e o mesmo conteúdo | Exatamente 1 | Teste de integração com reenvio; detecção por chave única no banco, não por consulta prévia |

## 4. Restrições

| ID | Restrição | Origem | Efeito no desenho |
|---|---|---|---|
| RE-01 | A queda do Consolidado não pode indisponibilizar o Controle de Lançamentos | Enunciado | Dependência unidirecional; nenhuma chamada síncrona de Lançamentos para Consolidado |
| RE-02 | Linguagem dominada pelo autor | Enunciado | C# sobre .NET 10 |
| RE-03 | Testes obrigatórios | Enunciado | Testes de domínio, arquitetura e integração com banco real |
| RE-04 | Documentação, código e instruções em repositório público | Enunciado | Nada essencial fora do repositório; ambiente local reproduzível por contêineres |
| RE-05 | Prazo de 5 horas de trabalho efetivo | Desafio | Código fino, documento como produto principal; diferenciais cortados com registro |

## 5. Premissas

| ID | Premissa | Impacto se não se confirmar |
|---|---|---|
| PR-01 | O pico de 50 req/s refere-se a consultas ao Consolidado, não a registros de lançamento | Se o registro também tiver pico relevante, a meta de capacidade de Lançamentos precisa ser definida e medida; o desenho comporta, mas não há número para ela hoje |
| PR-02 | "Perda de requisições" significa resposta de erro ou ausência de resposta, não dado perdido | Se significar dado perdido, a meta do RNF-02 não se aplica a consulta; o consolidado não perde dado, porque é reconstruível do registro |
| PR-03 | O comerciante aceita um saldo consolidado com atraso de poucos segundos, desde que sinalizado | Se exigir saldo exato no instante do registro, a separação assíncrona deixa de atender, e a consulta precisaria ler o registro de forma síncrona, o que reabre o ADR-0001 |
| PR-04 | O ambiente de avaliação roda contêineres Linux com Docker Compose | Sem Docker, a execução local exige instalar PostgreSQL e o broker manualmente |

## 6. Drivers arquiteturais

Em ordem de peso:

1. **Isolamento de falha** entre o registro (crítico) e o consolidado (sacrificável): RNF-01, RE-01. É o driver que decide o estilo.
2. **Integridade do fato financeiro**: nenhum lançamento perdido, duplicado ou alterado. RNF-04, RNF-06, RNF-07.
3. **Degradação controlada do consolidado** em pico, dentro do orçamento de 5%: RNF-02, RNF-03.
4. **Proporcionalidade**: 50 req/s é carga baixa. Nenhuma peça entra para resolver carga que não foi declarada.
5. **Prazo**: o desenho precisa ser demonstrável em código no tempo do desafio.

## 7. Questões abertas

Cada questão é lacuna do enunciado. A conduta provisória é aplicada no desenho e no código até haver resposta; a coluna final diz o que muda se a resposta for diferente.

| ID | Questão | Conduta provisória | O que muda se a resposta for outra |
|---|---|---|---|
| QA-01 | "Saldo diário consolidado" é o resultado do dia (entradas menos saídas do dia) ou o saldo acumulado de fechamento? | Expor os dois: `saldoDoDia` e `saldoAcumulado` (cartão 42). A questão continua aberta quanto a qual dos dois o negócio chama de "saldo consolidado" em relatórios | Se for só um, o outro campo pode ser retirado na próxima versão do contrato da API; nenhuma mudança de desenho |
| QA-02 | O caixa pode ficar negativo? | Permitido. É registro de fluxo de caixa, não conta com limite; recusar um débito real tornaria o registro falso | Validar saldo no registro exigiria que Lançamentos conhecesse o saldo, criando leitura de estado agregado no caminho crítico. Seria regra no domínio de Lançamentos, nunca consulta ao Consolidado |
| QA-03 | Lançamento retroativo é permitido? | Sim, com data do fato até hoje; o consolidado do dia afetado é recalculado pela projeção. Data futura é recusada | Se proibido, validação adicional no domínio. Se data futura for permitida (agendamento), surge o conceito de lançamento previsto, que é outro domínio |
| QA-04 | Um comerciante ou vários? | Vários caixas, identificados por `caixaId`. Sem autenticação no ambiente local, declarado | Com vários comerciantes reais, autenticação e autorização por caixa passam a ser obrigatórias (evolução listada na Arquitetura Alvo) |
| QA-05 | Qual moeda? | Somente real (BRL), duas casas decimais | Multimoeda exige moeda no lançamento e consolidado por moeda; nunca soma entre moedas |
| QA-06 | O caixa tem saldo de abertura (dinheiro já existente antes do primeiro lançamento)? | Não: o acumulado parte de zero. Saldo inicial entra como um crédito com a data de abertura | Se houver, campo de saldo de abertura por caixa, somado ao acumulado; sem mudança na projeção diária |
