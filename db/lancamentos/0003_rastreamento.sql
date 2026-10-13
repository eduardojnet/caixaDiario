-- Contexto de rastreamento W3C (traceparent) da requisicao que registrou o
-- lancamento. Gravado na mesma transacao da outbox, para o despachante
-- continuar o mesmo traco quando publicar, segundos ou horas depois
-- (ADR-0003). Nulo quando nao havia rastreamento ativo.
-- Primeira mudanca de esquema depois da entrega: aplicada pelo initdb so em
-- volume novo (docker compose down -v). Ver cartao 39, migracao versionada.
ALTER TABLE outbox ADD COLUMN traceparent varchar(55) NULL;
