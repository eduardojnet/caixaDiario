-- Esquema do servico Consolidado (ADR-0002).
-- Modelo de leitura reconstruivel: tudo aqui deriva dos eventos
-- LancamentoRegistrado v1. Aplicado pelo docker-entrypoint-initdb.d no compose
-- e pela fixture dos testes de integracao.

-- Idempotencia do projetor: um lancamento so e aplicado uma vez (RF-10).
CREATE TABLE lancamentos_aplicados (
    lancamento_id  uuid         PRIMARY KEY,
    caixa_id       varchar(64)  NOT NULL,
    data_do_fato   date         NOT NULL,
    aplicado_em    timestamptz  NOT NULL DEFAULT now()
);

CREATE TABLE saldo_diario (
    caixa_id        varchar(64)    NOT NULL,
    data            date           NOT NULL,
    total_creditos  numeric(16,2)  NOT NULL DEFAULT 0 CHECK (total_creditos >= 0),
    total_debitos   numeric(16,2)  NOT NULL DEFAULT 0 CHECK (total_debitos >= 0),
    quantidade      integer        NOT NULL DEFAULT 0 CHECK (quantidade >= 0),
    atualizado_ate  timestamptz    NOT NULL,
    PRIMARY KEY (caixa_id, data)
);

-- Uma linha: atraso medido no ultimo evento aplicado, base da marca 'defasado'.
CREATE TABLE estado_projecao (
    id                     smallint     PRIMARY KEY CHECK (id = 1),
    ultimo_registrado_em   timestamptz  NOT NULL,
    ultimo_aplicado_em     timestamptz  NOT NULL
);

REVOKE ALL ON lancamentos_aplicados, saldo_diario, estado_projecao FROM PUBLIC;
CREATE ROLE consolidado_app NOLOGIN;
GRANT SELECT, INSERT ON lancamentos_aplicados TO consolidado_app;
GRANT SELECT, INSERT, UPDATE ON saldo_diario TO consolidado_app;
GRANT SELECT, INSERT, UPDATE ON estado_projecao TO consolidado_app;
