# Domínios funcionais

**Público:** TI e backoffice. **Nível:** fronteiras, responsabilidades e fluxos.
**Requisito atendido:** mapeamento de domínios funcionais (O1).
**Data:** 2026-10-12.

## 1. Domínios

| Domínio | Classificação | Criticidade | Serviço do enunciado | Capacidades ([01](01-capacidades-de-negocio.md)) |
|---|---|---|---|---|
| **Controle de Lançamentos** | Núcleo | Crítico | Serviço de controle de lançamentos | C1.1 a C1.4, C2.1 |
| **Consolidado Diário** | Suporte | Sacrificável | Serviço de consolidado diário | C2.2, C2.3 |

**Por que núcleo e suporte.** O lançamento é o fato financeiro: sem ele não há caixa a conferir. O consolidado é uma visão derivada dos lançamentos; pode ser reconstruído a partir deles a qualquer momento. Perder um lançamento é perder informação; perder o consolidado é perder uma conveniência temporária.

**Por que "sacrificável".** O enunciado aceita até 5% de perda de requisições do consolidado em pico e exige que sua queda não afete os lançamentos. Sacrificável significa: em falha ou sobrecarga, é o consolidado que degrada, nunca o registro.

## 2. Fronteira e responsabilidade

### 2.1. Controle de Lançamentos

**É responsável por:**

- aceitar crédito e débito de um caixa, com valor, tipo, data do fato e descrição;
- validar o lançamento (valor positivo, tipo válido, data do fato não futura);
- impedir registro em dobro quando o mesmo envio se repete;
- guardar o lançamento de forma imutável: correção só por estorno;
- listar os lançamentos de um caixa em uma data;
- anunciar cada lançamento registrado, para quem quiser consumir.

**Não é responsável por:** calcular saldo, agregar por dia, saber quem consome o anúncio, saber se alguém consumiu.

### 2.2. Consolidado Diário

**É responsável por:**

- consumir os lançamentos anunciados e mantê-los agregados por caixa e data;
- aplicar cada lançamento uma única vez, mesmo que o anúncio chegue repetido;
- responder total de créditos, total de débitos, saldo do dia e quantidade de lançamentos;
- informar até quando o saldo está atualizado e se está defasado;
- proteger a si mesmo em pico: responder recusa rápida ao excedente, em vez de cair.

**Não é responsável por:** aceitar, validar ou corrigir lançamentos. Não tem caminho de escrita sobre o fato financeiro.

## 3. Assimetria de criticidade

| Aspecto | Controle de Lançamentos | Consolidado Diário |
|---|---|---|
| Natureza do dado | Fato original, fonte da verdade | Visão derivada, reconstruível |
| Efeito de uma queda | Comerciante não registra dinheiro que movimentou | Comerciante espera para conferir; nada se perde |
| Tolerância a perda de requisições | Nenhuma escrita aceita pode se perder | Até 5% em pico (enunciado) |
| Tolerância a atraso | Resposta imediata ao registro | Atraso curto aceitável, sinalizado ao usuário |
| Prioridade em conflito de recursos | Sempre preservado | Degrada primeiro |

## 4. Dependência unidirecional

```mermaid
flowchart LR
    L[Controle de Lançamentos<br/>núcleo, crítico] -- anuncia lançamento registrado --> C[Consolidado Diário<br/>suporte, sacrificável]
```

**Regra:** o Controle de Lançamentos **nunca** consulta, chama, referencia ou espera o Consolidado Diário. Nem em código, nem em configuração, nem em rede, nem em verificação de saúde. A informação flui só de Lançamentos para Consolidado.

**Efeito:** qualquer falha do Consolidado (processo fora, banco fora, sobrecarga, defeito) fica contida nele. O Controle de Lançamentos não tem como perceber essa falha, porque não depende de nada que o Consolidado ofereça. É essa regra, e só ela, que entrega o RNF-01 ([03-requisitos.md](03-requisitos.md)).

**Contrapartida:** o Consolidado depende de Lançamentos, de forma assíncrona. Se Lançamentos ou o canal de anúncio pararem, o consolidado não cai: fica desatualizado e informa isso.

## 5. Linguagem ubíqua

Termos usados igualmente no negócio, nos documentos e no código. Identificadores de código em português sem acento.

| Termo | Definição | No código |
|---|---|---|
| **Caixa** | O caixa de um comerciante, identificado de forma única. Um comerciante pode ter mais de um (QA-04) | `caixaId` |
| **Lançamento** | Registro de um movimento de dinheiro no caixa. Imutável depois de registrado | `Lancamento` |
| **Crédito** | Lançamento de entrada de dinheiro | `TipoLancamento.Credito`, `CREDITO` |
| **Débito** | Lançamento de saída de dinheiro | `TipoLancamento.Debito`, `DEBITO` |
| **Valor** | Quantia do lançamento, sempre positiva, em reais com duas casas. O sinal vem do tipo | `valor` |
| **Estorno** | Lançamento de tipo oposto que anula outro lançamento errado, referenciando-o. O lançamento original permanece no histórico | `estornoDe` |
| **Data do fato** | Dia em que o dinheiro efetivamente entrou ou saiu. Define em qual dia o lançamento conta no saldo | `dataDoFato` |
| **Data do registro** | Instante em que o lançamento foi gravado no sistema. Pode ser posterior à data do fato (QA-03) | `registradoEm` |
| **Saldo do dia** | Soma dos créditos menos soma dos débitos com a mesma data do fato, em um caixa | `saldoDoDia` |
| **Consolidado diário** | Conjunto de totais de um caixa em uma data: créditos, débitos, saldo do dia e quantidade | `SaldoDiario` |
| **Atualizado até** | Instante do último lançamento refletido no consolidado | `atualizadoAte` |
| **Defasado** | Indicação de que o consolidado pode não refletir lançamentos recentes, por atraso acima do limite do RNF-05 | `defasado` |
| **Chave de idempotência** | Identificador que o cliente envia com o lançamento para que um reenvio não gere um segundo registro | `Idempotency-Key` |
