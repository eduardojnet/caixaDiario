// RNF-02 e RNF-03: 50 requisicoes por segundo no Consolidado, com no maximo
// 5% de falha. Taxa de chegada constante: o k6 dispara 50 por segundo mesmo
// que o servico demore, como num pico real (nao espera a resposta anterior).
//
// Execucao, com o ambiente do compose no ar:
//   docker run --rm -i --network caixadiario_default \
//     -e ALVO=http://consolidado:8080 -e TOKEN_URL=http://keycloak:8080/realms/caixadiario/protocol/openid-connect/token \
//     grafana/k6:1.8.2 run - < tests/carga/consolidado-50rps.js
//
// O token OAuth2 (client credentials, cliente "contador", so leitura) e obtido
// uma vez no setup, fora da medicao, como um cliente real que reaproveita o
// token ate expirar (ADR-0004).

import http from 'k6/http';
import { check } from 'k6';

const ALVO = __ENV.ALVO || 'http://localhost:8081';
const TOKEN_URL = __ENV.TOKEN_URL || 'http://localhost:8180/realms/caixadiario/protocol/openid-connect/token';
const CLIENTE = __ENV.CLIENTE || 'contador';
const SEGREDO = __ENV.SEGREDO || 'contador-segredo-local';
const CAIXAS = ['loja-centro', 'loja-norte', 'loja-sul', 'quiosque'];

export const options = {
  scenarios: {
    pico: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: '60s',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
  thresholds: {
    // Perda maxima do enunciado: 5% (qualquer resposta nao 2xx conta, inclusive 429).
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<500'],
  },
};

export function setup() {
  const resposta = http.post(TOKEN_URL, {
    grant_type: 'client_credentials', client_id: CLIENTE, client_secret: SEGREDO,
  });
  if (resposta.status !== 200) {
    throw new Error(`token nao obtido: ${resposta.status}`);
  }
  return { token: resposta.json('access_token') };
}

export default function (dados) {
  const caixa = CAIXAS[Math.floor(Math.random() * CAIXAS.length)];
  const hoje = new Date().toISOString().slice(0, 10);
  const resposta = http.get(`${ALVO}/v1/caixas/${caixa}/consolidado/${hoje}`, {
    headers: { Authorization: `Bearer ${dados.token}` },
  });
  check(resposta, { 'status 200': (r) => r.status === 200 });
}
