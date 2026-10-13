#!/bin/sh
# Sobe o RabbitMQ e, quando ele termina de iniciar, aplica as credenciais por
# servico (permissoes.sh). A marca /tmp/configurado so existe depois disso: o
# healthcheck do compose a exige, para ninguem usar o broker sem permissoes.
set -eu
rm -f /tmp/configurado
docker-entrypoint.sh rabbitmq-server &
servidor=$!
trap 'kill -TERM "$servidor"; wait "$servidor"' TERM INT
# O cookie do Erlang e criado pelo servidor (usuario rabbitmq). Se a CLI rodar
# antes, como root, cria o cookie com o dono errado e o servidor nao sobe.
until [ -f "${HOME:-/var/lib/rabbitmq}/.erlang.cookie" ]; do sleep 1; done
su-exec rabbitmq rabbitmqctl -q await_startup
su-exec rabbitmq sh /opt/caixadiario/permissoes.sh
touch /tmp/configurado
wait "$servidor"
