# Contrato: `LancamentoRegistrado` versão 1

Evento publicado pelo Controle de Lançamentos a cada lançamento registrado, consumido pelo Consolidado Diário. Público: engenharia.

| | |
|---|---|
| Produtor | Lancamentos (despachante da outbox) |
| Consumidor | Consolidado (projetor) |
| Transporte | RabbitMQ, AMQP 0-9-1, exchange `caixadiario.lancamentos` (tipo `fanout`, durável), fila `consolidado.lancamento-registrado` (durável) |
| Formato | JSON, UTF-8, `content-type: application/json` |
| Tipo da mensagem | Propriedade AMQP `type` = `LancamentoRegistrado.v1`; `message-id` = `eventoId` |
| Entrega | **Ao menos uma vez.** Pode chegar repetido e fora de ordem |
| Publicação | Persistente, com confirmação do broker e `mandatory`: sem fila ligada à exchange, o evento é devolvido e permanece na outbox de Lancamentos até existir consumidor. Nenhum evento é descartado por falta de fila |
| Segurança | AMQP sobre TLS (porta 5671). Produtor publica com o usuário `lancamentos`, que só escreve na exchange; consumidor lê com o usuário `consolidado`, que não pode publicar nela (ADR-0004) |
| Mensagens irmãs | A mesma fila recebe o [`BatimentoDoDespachante` v1](batimento-do-despachante.v1.md), distinguido pela propriedade `type` |
| Compartilhamento de código | **Nenhum.** Cada serviço tem seu próprio tipo; os dois validam contra o exemplo abaixo |

## Campos

| Campo | Tipo JSON | Obrigatório | Regra |
|---|---|---|---|
| `eventoId` | string (UUID) | Sim | Único por evento; identifica a mensagem |
| `versao` | inteiro | Sim | `1` nesta versão |
| `lancamentoId` | string (UUID) | Sim | Identificador do lançamento; **chave de idempotência do consumidor** |
| `caixaId` | string | Sim | Identificador do caixa, 1 a 64 caracteres |
| `tipo` | string | Sim | `CREDITO` ou `DEBITO` |
| `valor` | número | Sim | Positivo, duas casas decimais, em reais (BRL). O sinal vem de `tipo` |
| `dataDoFato` | string (`AAAA-MM-DD`) | Sim | Dia em que o lançamento conta no saldo |
| `registradoEm` | string (ISO 8601 com fuso, UTC) | Sim | Instante do registro em Lancamentos |

## Exemplo

O mesmo conteúdo está em [lancamento-registrado.v1.exemplo.json](lancamento-registrado.v1.exemplo.json), arquivo lido pelos testes de contrato dos dois lados.

```json
{
  "eventoId": "0f8e2c5a-6b1d-4c3e-9a7f-2d4b6e8f0a1c",
  "versao": 1,
  "lancamentoId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "caixaId": "loja-centro",
  "tipo": "CREDITO",
  "valor": 150.75,
  "dataDoFato": "2026-10-12",
  "registradoEm": "2026-10-12T14:32:05.123Z"
}
```

## Semântica para o consumidor

1. **Idempotência:** aplicar o evento só se `lancamentoId` ainda não estiver em `lancamentos_aplicados`. A inserção e a soma acontecem na mesma transação; a chave única decide.
2. **Ordem:** irrelevante. A projeção é soma por `(caixaId, dataDoFato)`, comutativa.
3. **Confirmação:** `ack` só depois do `COMMIT`. Falha antes disso faz o broker reentregar, o que o item 1 torna inofensivo.
4. **Mensagem inválida** (JSON malformado, `versao` desconhecida): `reject` sem reenfileirar e registro em log. O lançamento continua íntegro em Lancamentos e o consolidado pode ser reconstruído.

## Regra de evolução

- **Mudança compatível** (campo opcional novo): mantém `versao: 1`. O consumidor ignora campos que não conhece.
- **Mudança incompatível** (remover campo, mudar tipo ou significado): nova versão, `LancamentoRegistrado.v2`, publicada **em paralelo** à v1 até todo consumidor migrar. A v1 só é desligada depois.
- Toda versão tem arquivo próprio nesta pasta, com exemplo.
