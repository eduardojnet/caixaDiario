-- Contexto de rastreamento W3C (traceparent) da requisicao que registrou o
-- lancamento. Gravado na mesma transacao da outbox, para o despachante
-- continuar o mesmo traco quando publicar, segundos ou horas depois
-- (ADR-0003). Nulo quando nao havia rastreamento ativo.
-- Primeira mudanca de esquema depois da entrega; motivou a migracao versionada
-- (cartao 39). O migrador aplica este script em volume existente, sem recriar.
ALTER TABLE outbox ADD COLUMN traceparent varchar(55) NULL;
