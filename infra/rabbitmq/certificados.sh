#!/bin/sh
# Gera, no primeiro "docker compose up", uma autoridade certificadora local e o
# certificado do broker (ADR-0004). Nada disso entra no repositorio: fica no
# volume "certificados". Se ja existir, nao faz nada.
set -eu
cd /certs
if [ -f broker.pem ]; then
    echo "certificados ja existem"
    exit 0
fi
openssl req -x509 -newkey rsa:2048 -nodes -days 825 -subj "/CN=CaixaDiario CA local" \
    -keyout ca-chave.pem -out ca.pem
openssl req -newkey rsa:2048 -nodes -subj "/CN=broker" -keyout broker-chave.pem -out broker.csr
printf "subjectAltName=DNS:broker,DNS:localhost\nextendedKeyUsage=serverAuth\n" > ext.cnf
openssl x509 -req -in broker.csr -CA ca.pem -CAkey ca-chave.pem -CAcreateserial -days 825 \
    -extfile ext.cnf -out broker.pem
rm -f broker.csr ext.cnf ca.srl ca-chave.pem
# Leitura pelo usuario do RabbitMQ no outro contêiner. Ambiente local.
chmod 644 ca.pem broker.pem broker-chave.pem
echo "certificados gerados"
