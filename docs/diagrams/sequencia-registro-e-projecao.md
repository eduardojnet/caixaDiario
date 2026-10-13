# Sequência: registro, despacho, projeção e consulta

Ordem das interações de um lançamento, do registro à consulta do saldo. Público: engenharia. Convenções em [README](README.md). Contrato do evento em [lancamento-registrado.v1.md](../contratos/lancamento-registrado.v1.md).

```mermaid
sequenceDiagram
    autonumber
    actor C as Comerciante
    participant L as API Lancamentos
    participant LDB as Banco de lançamentos
    participant D as Despachante da outbox
    participant B as Broker
    participant P as Projetor
    participant CDB as Banco do consolidado
    participant Q as API Consolidado

    rect rgb(235, 243, 252)
    note over C,LDB: Registro: caminho crítico, não depende de nada à direita
    C->>L: POST /v1/caixas/{caixaId}/lancamentos<br/>Idempotency-Key: k
    L->>L: valida domínio (valor > 0, tipo, data não futura)
    L->>LDB: BEGIN
    L->>LDB: INSERT lancamentos (chave única: caixa_id, chave_idempotencia)
    alt chave nova
        L->>LDB: INSERT outbox (evento LancamentoRegistrado v1)
        L->>LDB: COMMIT
        L-->>C: 201 Created
    else chave já usada (violação de chave única)
        L->>LDB: ROLLBACK
        L->>LDB: SELECT lançamento original pela chave
        alt mesmo conteúdo
            L-->>C: 200 OK + Idempotency-Replayed: true
        else conteúdo diferente
            L-->>C: 422 Unprocessable Entity
        end
    end
    end

    note over D,B: Despacho: assíncrono, com retentativa e espera crescente
    loop a cada intervalo
        D->>LDB: SELECT pendentes FOR UPDATE SKIP LOCKED
        D->>B: publica com confirmação
        B-->>D: confirmado
        D->>LDB: UPDATE outbox SET publicado_em
    end

    note over B,CDB: Projeção: ao menos uma vez, idempotente
    B->>P: entrega LancamentoRegistrado
    P->>CDB: BEGIN
    P->>CDB: INSERT lancamentos_aplicados (lancamento_id) ON CONFLICT DO NOTHING
    alt primeira entrega
        P->>CDB: UPSERT saldo_diario (soma crédito ou débito, atualizado_ate)
    else reentrega
        P->>P: descarta, nada a somar
    end
    P->>CDB: COMMIT
    P-->>B: ack

    note over C,Q: Consulta: pode estar defasada, e diz isso
    C->>Q: GET /v1/caixas/{caixaId}/consolidado/{data}
    Q->>Q: limitador de taxa (excedente: 429)
    Q->>Q: cache curto
    Q->>CDB: SELECT saldo_diario
    Q-->>C: 200 totais, saldoDoDia, atualizadoAte, defasado
```

## Onde está cada garantia

| Passo | Garantia | Requisito |
|---|---|---|
| 4 a 6 | Lançamento e evento gravados juntos ou nenhum dos dois | RNF-04, RF-07 |
| 4, 8 a 11 | Reenvio detectado pela chave única no banco, não por consulta prévia | RNF-07, RF-04 |
| 12 a 15 | Despacho fora do caminho do registro; broker fora não afeta o `201` | RNF-01 |
| 16 a 22 | Evento repetido não soma duas vezes; `ack` só depois do `COMMIT` | RF-10, RNF-04 |
| 23 a 27 | Consulta protegida por limitador; atraso sinalizado por `atualizadoAte` e `defasado` | RNF-02, RNF-03, RNF-05 |

O papel da aplicação em Lancamentos não tem `UPDATE` nem `DELETE` na tabela `lancamentos` (RNF-06). O `UPDATE` do passo 15 atinge só a tabela `outbox`.

## Estado

Todos os passos: **especificado**, implementação no Bloco 4. A fonte do estado é o [ESTADO.md](../ESTADO.md).
