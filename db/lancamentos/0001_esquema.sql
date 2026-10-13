-- Esquema do servico Lancamentos (ADR-0002).
-- Aplicado pelo docker-entrypoint-initdb.d no compose e pela fixture dos
-- testes de integracao: o mesmo arquivo nos dois lugares.
--
-- As invariantes moram aqui, nao no codigo (regra 7):
--   * idempotencia: chave unica (caixa_id, chave_idempotencia);
--   * imutabilidade: o papel da aplicacao so tem SELECT e INSERT em lancamentos;
--   * estorno: no maximo um estorno por lancamento, referenciando um existente.

CREATE TABLE lancamentos (
    id                  uuid          PRIMARY KEY,
    caixa_id            varchar(64)   NOT NULL CHECK (length(caixa_id) BETWEEN 1 AND 64),
    tipo                varchar(7)    NOT NULL CHECK (tipo IN ('CREDITO', 'DEBITO')),
    valor               numeric(14,2) NOT NULL CHECK (valor > 0),
    data_do_fato        date          NOT NULL,
    descricao           varchar(200)  NOT NULL DEFAULT '',
    estorno_de          uuid          NULL REFERENCES lancamentos (id),
    chave_idempotencia  varchar(100)  NOT NULL,
    hash_requisicao     char(64)      NOT NULL,
    registrado_em       timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT uq_lancamentos_idempotencia UNIQUE (caixa_id, chave_idempotencia),
    CONSTRAINT uq_lancamentos_estorno UNIQUE (estorno_de)
);

CREATE INDEX ix_lancamentos_caixa_data ON lancamentos (caixa_id, data_do_fato);

-- Outbox transacional: gravada na mesma transacao do lancamento.
CREATE TABLE outbox (
    id            bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    evento_id     uuid          NOT NULL UNIQUE,
    tipo_evento   varchar(100)  NOT NULL,
    payload       jsonb         NOT NULL,
    criado_em     timestamptz   NOT NULL DEFAULT now(),
    publicado_em  timestamptz   NULL
);

CREATE INDEX ix_outbox_pendentes ON outbox (id) WHERE publicado_em IS NULL;

-- Papel da aplicacao. Sem LOGIN aqui: a senha vem de variavel de ambiente,
-- aplicada por 0002_senha_aplicacao.sh no compose e pela fixture nos testes.
REVOKE ALL ON lancamentos, outbox FROM PUBLIC;
CREATE ROLE lancamentos_app NOLOGIN;
GRANT SELECT, INSERT ON lancamentos TO lancamentos_app;
GRANT SELECT, INSERT ON outbox TO lancamentos_app;
-- O despachante marca o evento como publicado; nenhuma outra coluna e alteravel.
GRANT UPDATE (publicado_em) ON outbox TO lancamentos_app;
