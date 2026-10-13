#!/bin/sh
# Credencial do broker por servico, com privilegio minimo (ADR-0004).
# Executado dentro do contêiner do RabbitMQ: pelo entrypoint.sh no compose e
# pelos testes de integracao. Idempotente: pode rodar a cada subida.
#
# Permissoes do RabbitMQ: configure (declarar), write (publicar e ligar),
# read (consumir e ler da exchange ao ligar).
#   lancamentos: declara e publica so na exchange caixadiario.lancamentos;
#                nao le nada.
#   consolidado: declara a exchange e a propria fila, liga a fila e consome;
#                nao tem write na exchange, entao nao consegue publicar nela.
set -eu

: "${BROKER_LANCAMENTOS_USER:=lancamentos}"
: "${BROKER_CONSOLIDADO_USER:=consolidado}"
: "${BROKER_LANCAMENTOS_PASSWORD:?senha do usuario de Lancamentos ausente}"
: "${BROKER_CONSOLIDADO_PASSWORD:?senha do usuario de Consolidado ausente}"

garantir_usuario() {
    if rabbitmqctl -q list_users | cut -f1 | grep -qx "$1"; then
        rabbitmqctl -q change_password "$1" "$2"
    else
        rabbitmqctl -q add_user "$1" "$2"
    fi
}

garantir_usuario "$BROKER_LANCAMENTOS_USER" "$BROKER_LANCAMENTOS_PASSWORD"
rabbitmqctl -q set_permissions -p / "$BROKER_LANCAMENTOS_USER" \
    '^caixadiario\.lancamentos$' '^caixadiario\.lancamentos$' '^$'

garantir_usuario "$BROKER_CONSOLIDADO_USER" "$BROKER_CONSOLIDADO_PASSWORD"
rabbitmqctl -q set_permissions -p / "$BROKER_CONSOLIDADO_USER" \
    '^(caixadiario\.lancamentos|consolidado\..+)$' '^consolidado\..+$' '^(caixadiario\.lancamentos|consolidado\..+)$'

echo "permissoes do broker aplicadas"
