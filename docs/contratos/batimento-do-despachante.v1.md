# Contrato: `BatimentoDoDespachante` versão 1

Sinal de vida publicado pelo despachante de Lancamentos a intervalo fixo, com o estado da outbox. Permite ao Consolidado distinguir "não houve lançamento" de "a publicação parou" (lacuna L-05, cartão 43). Público: engenharia.

| | |
|---|---|
| Produtor | Lancamentos (despachante da outbox), a cada `Despacho:IntervaloDoBatimento` (padrão 5 s) |
| Consumidor | Consolidado (projetor) |
| Transporte | Mesma exchange e fila de [`LancamentoRegistrado` v1](lancamento-registrado.v1.md), AMQP sobre TLS |
| Tipo da mensagem | Propriedade AMQP `type` = `BatimentoDoDespachante.v1`. O consumidor distingue pelo `type`, não pelo corpo |
| Entrega | **Sem garantia, de propósito.** Não persistente, sem `mandatory` e com expiração de três intervalos: batimento velho não tem valor e não deve acumular na fila com o Consolidado fora |
| Compartilhamento de código | Nenhum; os dois lados validam contra o exemplo abaixo |

## Campos

| Campo | Tipo JSON | Regra |
|---|---|---|
| `versao` | inteiro | `1` |
| `emitidoEm` | string ISO 8601 UTC | Instante da medida. Informativo: o consumidor usa o próprio relógio, na chegada, para julgar atraso, e não depende do relógio do produtor |
| `outboxPendentes` | inteiro ≥ 0 | Eventos na outbox ainda não publicados |
| `outboxIdadeMaisAntigaSegundos` | número ≥ 0 | Idade do evento pendente mais antigo; 0 sem pendência |

## Exemplo

Em [batimento-do-despachante.v1.exemplo.json](batimento-do-despachante.v1.exemplo.json):

```json
{
  "versao": 1,
  "emitidoEm": "2026-10-12T14:32:10.000Z",
  "outboxPendentes": 3,
  "outboxIdadeMaisAntigaSegundos": 1.5
}
```

## Semântica para o consumidor

A resposta do consolidado traz `defasado: true` quando qualquer condição abaixo vale (RNF-05):

1. O projetor não está conectado ao broker.
2. Nenhum batimento chegou há mais de `Consulta:LimiteDoBatimento` (padrão 15 s, três intervalos): Lancamentos, seu banco ou o caminho até o broker parou.
3. O último batimento informa evento pendente na outbox mais velho que o limite do RNF-05: a publicação está atrasada na origem.
4. Há fila acumulada no Consolidado e o último evento aplicado chegou com atraso acima do limite.

Ao conectar, o projetor concede um batimento de carência, para não marcar defasado antes do primeiro sinal possível.
