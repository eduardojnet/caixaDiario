#!/bin/sh
# Habilita o login do papel da aplicacao com a senha da variavel de ambiente.
# A senha nunca entra no repositorio (.env fora do versionamento).
set -eu
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -v senha="$APP_DB_PASSWORD" <<'SQL'
ALTER ROLE lancamentos_app LOGIN PASSWORD :'senha';
SQL
