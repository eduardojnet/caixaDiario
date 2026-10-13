// RNF-02 e RNF-03: 50 requisicoes por segundo no Consolidado, com no maximo
// 5% de falha. Taxa de chegada constante: o k6 dispara 50 por segundo mesmo
// que o servico demore, como num pico real (nao espera a resposta anterior).
//
// Execucao, com o ambiente do compose no ar:
//   docker run --rm -i --network caixadiario_default \
//     -e ALVO=http://consolidado:8080 grafana/k6:1.8.2 run - < tests/carga/consolidado-50rps.js

import http from 'k6/http';
import { check } from 'k6';

const ALVO = __ENV.ALVO || 'http://localhost:8081';
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

export default function () {
  const caixa = CAIXAS[Math.floor(Math.random() * CAIXAS.length)];
  const hoje = new Date().toISOString().slice(0, 10);
  const resposta = http.get(`${ALVO}/v1/caixas/${caixa}/consolidado/${hoje}`);
  check(resposta, { 'status 200': (r) => r.status === 200 });
}
