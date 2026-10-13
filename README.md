# CaixaDiario

[![CI](https://github.com/eduardojnet/caixaDiario/actions/workflows/ci.yml/badge.svg)](https://github.com/eduardojnet/caixaDiario/actions/workflows/ci.yml)

Controle de fluxo de caixa diário de um comerciante: lançamentos de crédito e débito e saldo diário consolidado. Solução de um desafio técnico de Arquiteto de Soluções.

- [Como a aplicação funciona](#como-a-aplicação-funciona)
- [Como rodar localmente](#como-rodar-localmente)
- [Testes](#testes)
- [Documentação](#documentação)
- [Estado atual](#estado-atual)
- [O que faria com mais tempo](#o-que-faria-com-mais-tempo)

---

## Como a aplicação funciona

### Resumo

O comerciante registra cada entrada e saída de dinheiro durante o expediente e, no fim do dia, confere o resultado. A solução separa essas duas atividades: o registro é tratado como operação crítica e nunca depende do relatório de saldo; o relatório é calculado automaticamente em segundo plano. Se o relatório ficar fora do ar, o comerciante continua registrando, nenhum lançamento se perde, e o saldo se atualiza sozinho quando o relatório volta. O custo aceito é que o saldo consultado pode estar alguns segundos atrás do último lançamento, e a consulta sempre informa até quando está atualizada.

### O que muda para quem confere o caixa

- Cada lançamento entra uma única vez, mesmo que o envio se repita por falha de conexão.
- O saldo do dia (total de entradas, total de saídas e resultado) é calculado sem trabalho manual.
- Lançamento errado é corrigido por estorno, que fica no histórico; nada é apagado.
- Quando o saldo ainda não reflete os últimos lançamentos, a consulta diz isso (`defasado`) e até que instante está atualizada (`atualizadoAte`).

### Como funciona por dentro

São dois serviços, cada um com seu banco PostgreSQL, ligados por um broker RabbitMQ:

| Serviço | Papel | Criticidade |
|---|---|---|
| **Lancamentos** (`:8080`) | Registra créditos e débitos com idempotência e lista os lançamentos do dia | Crítico: não pode cair |
| **Consolidado** (`:8081`) | Responde o saldo diário consolidado a partir de um modelo de leitura | Sacrificável: tolera até 5% de perda em pico |

**Por que dois serviços.** O enunciado exige que o registro não fique indisponível se o consolidado cair. **O volume não justifica a separação; o isolamento de falha justifica**: 50 requisições por segundo cabem num único processo, mas um único processo não impede que uma falha do consolidado derrube o registro. A dependência é de mão única: Lancamentos nunca chama, lê ou espera o Consolidado ([ADR-0001](docs/adr/ADR-0001-estilo-arquitetural.md)).

**Caminho de um lançamento:**

1. `POST` em Lancamentos com `Idempotency-Key`. O lançamento e o evento `LancamentoRegistrado` são gravados **na mesma transação** (outbox). Resposta `201`.
2. Um despachante, em segundo plano, publica os eventos pendentes no broker. Broker fora: os eventos esperam na outbox, e o registro continua respondendo `201`.
3. O projetor do Consolidado consome o evento e soma ao saldo do dia, **uma única vez** por lançamento, mesmo que o evento chegue repetido.
4. `GET` no Consolidado devolve os totais do dia, `atualizadoAte` e `defasado`. Em pico, um limitador recusa o excedente com `429` em vez de deixar o serviço cair.

Diagrama de contêineres: [C2](docs/diagrams/c2-conteineres.md). Sequência completa: [registro e projeção](docs/diagrams/sequencia-registro-e-projecao.md). Decisões de tecnologia: [ADR-0002](docs/adr/ADR-0002-plataforma-armazenamento-e-integracao.md).

---

## Como rodar localmente

### Pré-requisitos

| Ferramenta | Versão verificada | Para quê |
|---|---|---|
| Docker Engine | 29.8.2 | Rodar os contêineres |
| Docker Compose | v5.6.0 (plugin `docker compose`) | Subir o ambiente |
| curl e sed | qualquer | Exemplos de requisição |
| .NET SDK | 10.0.401 | **Só** para rodar os testes; a aplicação roda sem ele |

As portas `8080`, `8081`, `8180`, `15672` e `18888` precisam estar livres. Nenhuma senha precisa ser configurada: o compose usa padrões locais. Para trocá-las, copie `.env.example` para `.env`.

### Passos

**1. Clonar o repositório.**

```bash
git clone https://github.com/eduardojnet/caixaDiario.git
cd caixaDiario
```

Resultado esperado: pasta `caixaDiario` com `docker-compose.yml` na raiz.

**2. Subir o ambiente.**

```bash
docker compose up --build -d
```

Resultado esperado: na primeira vez, as imagens são construídas e baixadas (alguns minutos). Confira com `docker compose ps -a`: `broker`, `keycloak`, `lancamentos-db` e `consolidado-db` aparecem `(healthy)`; `lancamentos`, `consolidado` e `painel`, `Up`; `certificados` aparece `Exited (0)`, porque só gera os certificados TLS do broker e termina. O Keycloak leva cerca de 30 segundos para ficar `(healthy)`.

**3. Conferir que as duas APIs estão prontas.**

```bash
curl -s -o /dev/null -w "%{http_code}\n" localhost:8080/health/ready
curl -s -o /dev/null -w "%{http_code}\n" localhost:8081/health/ready
```

Resultado esperado: `200` nas duas linhas. Se vier `000`, aguarde alguns segundos: a API ainda está subindo.

**4. Obter um token de acesso.** As APIs exigem OAuth2 client credentials ([ADR-0004](docs/adr/ADR-0004-seguranca-de-integracao.md)). O cliente local `caixa-loja` pode registrar e consultar:

```bash
TOKEN=$(curl -s -X POST localhost:8180/realms/caixadiario/protocol/openid-connect/token \
  -d grant_type=client_credentials -d client_id=caixa-loja -d client_secret=caixa-loja-segredo-local \
  | sed -E 's/.*"access_token":"([^"]+)".*/\1/')
echo "${TOKEN:0:20}..."
```

Resultado esperado: o começo de um token JWT, como `eyJhbGciOiJSUzI1NiIs...`. O token vale 5 minutos; se um passo seguinte responder `401`, repita este.

**5. Registrar um crédito e um débito.**

```bash
HOJE=$(TZ=America/Sao_Paulo date +%F)

curl -s -X POST localhost:8080/v1/caixas/loja-centro/lancamentos \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -H 'Idempotency-Key: venda-001' \
  -d "{\"tipo\":\"CREDITO\",\"valor\":150.75,\"dataDoFato\":\"$HOJE\",\"descricao\":\"venda balcao\"}"; echo

curl -s -X POST localhost:8080/v1/caixas/loja-centro/lancamentos \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -H 'Idempotency-Key: compra-001' \
  -d "{\"tipo\":\"DEBITO\",\"valor\":50.25,\"dataDoFato\":\"$HOJE\",\"descricao\":\"fornecedor\"}"; echo
```

Resultado esperado: dois JSON com `id`, `tipo`, `valor`, `dataDoFato` e `registradoEm` (resposta `201 Created`).

**6. Consultar o saldo do dia.**

```bash
curl -s -H "Authorization: Bearer $TOKEN" localhost:8081/v1/caixas/loja-centro/consolidado/$HOJE; echo
```

Resultado esperado:

```json
{"caixaId":"loja-centro","data":"<hoje>","totalCreditos":150.75,"totalDebitos":50.25,"saldoDoDia":100.50,"quantidade":2,"atualizadoAte":"<instante>","defasado":false}
```

Se `quantidade` vier menor que 2, repita depois de um segundo: a projeção é assíncrona.

**7. Ver a idempotência.** Repita exatamente o primeiro `POST` do passo 5, filtrando os cabeçalhos:

```bash
curl -s -i -X POST localhost:8080/v1/caixas/loja-centro/lancamentos \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -H 'Idempotency-Key: venda-001' \
  -d "{\"tipo\":\"CREDITO\",\"valor\":150.75,\"dataDoFato\":\"$HOJE\",\"descricao\":\"venda balcao\"}" \
  | grep -iE '^HTTP|^Idempotency-Replayed|"id"'
```

Resultado esperado: `HTTP/1.1 200 OK`, `Idempotency-Replayed: true` e o corpo com o mesmo `id` do primeiro envio. A mesma chave com outro corpo responde `422`. O saldo não muda.

**8. Listar os lançamentos do dia.**

```bash
curl -s -H "Authorization: Bearer $TOKEN" "localhost:8080/v1/caixas/loja-centro/lancamentos?data=$HOJE"; echo
```

Resultado esperado: lista JSON com os dois lançamentos.

### Demonstração do RNF-01 em três comandos

Com o `TOKEN` e o `HOJE` do passo 5, o registro continua funcionando com o Consolidado **e o banco dele** fora do ar:

```bash
docker compose stop consolidado consolidado-db
curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:8080/v1/caixas/loja-centro/lancamentos -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -H 'Idempotency-Key: rnf01-001' -d "{\"tipo\":\"CREDITO\",\"valor\":20,\"dataDoFato\":\"$HOJE\"}"
docker compose start consolidado-db consolidado
```

Resultado esperado: o segundo comando imprime `201`. Depois do terceiro, em poucos segundos, o passo 6 mostra `quantidade` 3 e `saldoDoDia` 120.50: o evento esperou na fila e foi aplicado quando o Consolidado voltou. O mesmo vale para o broker: `docker compose stop broker`, registre, e os eventos esperam na outbox até `docker compose start broker`.

### Segurança: o que é recusado

O cliente local `contador` só tem leitura. Com o `HOJE` do passo 5:

```bash
CONTADOR=$(curl -s -X POST localhost:8180/realms/caixadiario/protocol/openid-connect/token \
  -d grant_type=client_credentials -d client_id=contador -d client_secret=contador-segredo-local \
  | sed -E 's/.*"access_token":"([^"]+)".*/\1/')
curl -s -o /dev/null -w "sem token: %{http_code}\n" -X POST localhost:8080/v1/caixas/loja-centro/lancamentos -H 'Content-Type: application/json' -H 'Idempotency-Key: x-1' -d "{\"tipo\":\"CREDITO\",\"valor\":1,\"dataDoFato\":\"$HOJE\"}"
curl -s -o /dev/null -w "contador registrando: %{http_code}\n" -X POST localhost:8080/v1/caixas/loja-centro/lancamentos -H "Authorization: Bearer $CONTADOR" -H 'Content-Type: application/json' -H 'Idempotency-Key: x-2' -d "{\"tipo\":\"CREDITO\",\"valor\":1,\"dataDoFato\":\"$HOJE\"}"
curl -s -o /dev/null -w "contador consultando: %{http_code}\n" -H "Authorization: Bearer $CONTADOR" localhost:8081/v1/caixas/loja-centro/consolidado/$HOJE
```

Resultado esperado: `sem token: 401`, `contador registrando: 403`, `contador consultando: 200`. No broker, cada serviço tem o próprio usuário: Lancamentos só publica, Consolidado só consome, e a conexão usa TLS na porta 5671 (a 5672, sem TLS, está fechada).

### Exemplos de recusa

| Situação | Resposta |
|---|---|
| Sem token, token expirado ou de outro emissor | `401` |
| Token sem o escopo da operação | `403` |
| Sem `Idempotency-Key` | `400` |
| `valor` zero ou negativo, `tipo` diferente de `CREDITO`/`DEBITO`, `dataDoFato` futura | `400` com a lista de erros |
| Mesma chave, corpo diferente | `422` |
| Estorno (`"estornoDe": "<id>"`) com tipo igual, valor diferente ou lançamento já estornado | `422` |
| Consolidado acima do limite de taxa | `429` |

Consoles locais, sem valor fora desta máquina: gerência do RabbitMQ em http://localhost:15672 (usuário `caixadiario`, senha `caixadiario_local`) e administração do Keycloak em http://localhost:8180 (usuário `admin`, senha `admin_local`).

### Ver o traço de um lançamento no painel

O painel de observabilidade fica em http://localhost:18888 (sem login, só no ambiente local). Com o `TOKEN` do passo 4, envie um lançamento com um identificador de traço conhecido:

```bash
TRACO=$(openssl rand -hex 16)
curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:8080/v1/caixas/loja-centro/lancamentos \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -H 'Idempotency-Key: traco-001' \
  -H "traceparent: 00-$TRACO-$(openssl rand -hex 8)-01" \
  -d "{\"tipo\":\"CREDITO\",\"valor\":10,\"dataDoFato\":\"$(TZ=America/Sao_Paulo date +%F)\"}"
echo "http://localhost:18888/traces/detail/$TRACO"
```

Resultado esperado: `201` e um endereço; aberto no navegador, mostra um traço só, com `POST` e `outbox publicar` no serviço `lancamentos` e `projetor aplicar` no `consolidado`, mais os comandos do PostgreSQL. Em **Metrics**, as métricas `caixadiario.*`: outbox pendente, defasagem e fila do projetor, eventos aplicados.

### Se algo der errado

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| `Bind for 0.0.0.0:8080 failed: port is already allocated` (ou 8081, 8180, 15672, 18888) | Outra aplicação na porta | Pare a outra aplicação, ou troque a porta da esquerda em `ports:` no `docker-compose.yml` |
| `curl` responde `000` logo após subir | API ainda iniciando | Aguarde alguns segundos e repita; `docker compose logs lancamentos` mostra a subida |
| `TOKEN` vazio ou o `curl` do passo 4 não responde | Keycloak ainda iniciando | Aguarde `keycloak` ficar `(healthy)` em `docker compose ps` e repita o passo 4 |
| `401` com token | Token expirado (5 minutos) | Repita o passo 4 |
| `POST` responde `400` com "dataDoFato nao pode ser futura" | Data calculada em outro fuso | Use `TZ=America/Sao_Paulo date +%F`, como nos exemplos |
| Saldo não aparece | Projeção ainda não aplicada, ou Consolidado parado | `docker compose ps`; `docker compose logs consolidado`; a resposta traz `defasado: true` se o projetor estiver sem broker |
| Erro de autenticação no banco ou no broker depois de trocar senhas no `.env` | Bancos e broker guardam a senha da primeira subida | Recomece do zero (abaixo) |

### Parar e começar do zero

```bash
docker compose down        # para e remove os contêineres; os dados ficam nos volumes
docker compose down -v     # também apaga os volumes: bancos, fila e certificados voltam do zero
```

Depois de `down -v`, o passo 2 recria tudo, inclusive esquemas e certificados.

---

## Testes

```bash
dotnet test                 # 56 testes, sem erro e sem aviso
```

Pré-requisitos: .NET SDK 10 e **Docker em execução**. Os testes de integração sobem PostgreSQL e RabbitMQ reais (Testcontainers) e aplicam os mesmos scripts de esquema que o compose usa.

| Projeto | Testes | O que verifica |
|---|---|---|
| `CaixaDiario.Dominio.Tests` | 25 | Validação do lançamento: valor positivo com duas casas, limites de tamanho e de valor, tipo, data futura recusada, regra do estorno |
| `CaixaDiario.Arquitetura.Tests` | 4 | NetArchTest: nenhum tipo de Lancamentos referencia Consolidado; sem assembly compartilhado; domínio sem infraestrutura |
| `CaixaDiario.Integracao.Tests` | 27 | **RNF-01** (Lancamentos sem Consolidado e sem broker responde `201`, a outbox acumula, a métrica mostra o acúmulo, e a outbox esvazia quando o broker volta), traço único do `POST` à projeção, idempotência sequencial e com 10 envios simultâneos, estorno, imutabilidade por privilégio do banco, projetor idempotente, reconciliação ponta a ponta, contrato do evento nos dois lados, segurança (`401` sem token ou com token inválido, `403` sem escopo, usuário do Consolidado recusado ao publicar no broker) |

**Por que banco real.** Idempotência sob envio simultâneo depende da chave única; imutabilidade depende de privilégio. Um repositório em memória passaria na implementação ingênua.

**Carga (RNF-02).** Com o ambiente no ar:

```bash
docker run --rm -i --network caixadiario_default -e ALVO=http://consolidado:8080 \
  -e TOKEN_URL=http://keycloak:8080/realms/caixadiario/protocol/openid-connect/token \
  grafana/k6:1.8.2 run - < tests/carga/consolidado-50rps.js
```

Taxa de chegada constante de 50 req/s por 60 s, limite `http_req_failed < 5%`. O script obtém um token do cliente `contador` antes de medir. Resultado medido em 2026-10-12, com autenticação: 3001 requisições, **0,00% de falha**, p95 de 3,3 ms (1,56 ms antes da autenticação).

**Integração contínua.** A cada push, o [GitHub Actions](.github/workflows/ci.yml) compila sem avisos, roda os testes (com PostgreSQL e RabbitMQ reais), exige cobertura de linha do domínio de 85% (medida: 96,36%) e roda o teste de mutação do domínio com limite de 85% (Stryker.NET 5.0.0, medido: 98,21%). Para rodar a mutação localmente: `dotnet tool restore` e, em `tests/CaixaDiario.Dominio.Tests`, `dotnet stryker`.

---

## Documentação

| Documento | Conteúdo |
|---|---|
| [01 Capacidades de negócio](docs/01-capacidades-de-negocio.md) | Cadeia de valor do comerciante, mapa de capacidades com criticidade |
| [02 Domínios funcionais](docs/02-dominios-funcionais.md) | Controle de Lançamentos e Consolidado Diário, fronteiras, dependência unidirecional, glossário |
| [03 Requisitos](docs/03-requisitos.md) | RF, RNF com métrica, meta e medição, restrições, premissas, drivers, questões abertas |
| [04 Arquitetura Alvo](docs/04-arquitetura-alvo.md) | Visões de negócio, operacional e técnica; degradação; observabilidade; segurança; trade-offs |
| [05 Arquitetura de Transição](docs/05-arquitetura-de-transicao.md) | Substituição de um legado hipotético em cinco fases de estrangulamento, com critério de avanço e caminho de volta (especificado) |
| [06 Estimativa de Custos](docs/06-estimativa-de-custos.md) | Custo mensal em nuvem (AWS São Paulo, preços datados): ≈ US$ 196 na topologia recomendada, ≈ US$ 52 numa VM única; licenças sem custo |
| [ADRs](docs/adr/README.md) | ADR-0001 estilo arquitetural; ADR-0002 plataforma, armazenamento e integração |
| [Diagramas](docs/diagrams/README.md) | C1 contexto, C2 contêineres, C3 componentes, sequência de registro e projeção |
| [Contrato do evento](docs/contratos/lancamento-registrado.v1.md) | `LancamentoRegistrado` v1, com exemplo JSON e regra de versão |
| [Estado do projeto](docs/ESTADO.md) | O que está implementado, o que está só especificado, questões abertas, histórico |

---

## Estado atual

| Item | Estado |
|---|---|
| Registro idempotente, estorno, lista do dia | Implementado, com testes |
| Outbox transacional e despachante | Implementado, com testes |
| Projetor idempotente e consulta do consolidado | Implementado, com testes |
| Cache curto, limitador com `429`, marca `defasado` | Implementado; limitador verificado em rajada (excedente recebe `429`) |
| RNF-01: registro sem Consolidado e sem broker | Implementado e provado por teste e pela demonstração acima |
| RNF-02/RNF-03: 50 req/s com até 5% de perda | Medido: 0,00% de falha |
| RNF-04 consistência, RNF-06 imutabilidade, RNF-07 idempotência | Implementado, com testes contra banco real |
| RNF-05: defasagem até 5 s | Marca implementada; meta proposta, não medida com instrumento `[NVI]` |
| Traço distribuído, métricas e logs (OpenTelemetry) com painel local | Implementado ([ADR-0003](docs/adr/ADR-0003-observabilidade.md)); traço do `POST` à projeção testado |
| Autenticação OAuth2 na API, credencial de broker por serviço, TLS no broker | Implementado ([ADR-0004](docs/adr/ADR-0004-seguranca-de-integracao.md)), com testes de recusa |
| Integração contínua: build, testes, cobertura e mutação do domínio | Implementado: [GitHub Actions](.github/workflows/ci.yml), verde |
| Arquitetura de transição a partir de legado | Especificado: [05](docs/05-arquitetura-de-transicao.md), sobre hipótese de legado declarada |
| Estimativa de custo de infraestrutura e licenças | Especificado: [06](docs/06-estimativa-de-custos.md), preços da API pública da AWS em 2026-10-12 |
| Integração com maquininha e banco | Especificado, fora do escopo |

Detalhe e evidência de cada item em [docs/ESTADO.md](docs/ESTADO.md).

---

## Evidência de implementação do mesmo mecanismo

O componente de livro-razão desta solução, no lado de escrita, aplica o mesmo mecanismo que implementei e medi em outro projeto, de domínio diferente (conta corrente bancária, com posição consolidada em instante arbitrário): https://github.com/eduardojnet/PacioliBank. Os números são verificáveis no repositório: build sem avisos, 237 testes passando, cobertura de linha do domínio de 94,11% e escore de mutação de 86,96%, ambos acima do limite de 85% que o CI daquele projeto exige (estado de 2026-10-06). Cito como evidência de que o desenho aqui proposto é implementável, não como implementação desta solução.

---

## O que faria com mais tempo

Cada item tem gatilho declarado; nenhum é necessário para os requisitos obrigatórios.

| Evolução | Gatilho |
|---|---|
| Autorização por caixa (um comerciante não lê o caixa de outro), HTTPS no servidor de autorização e segredos em cofre | Mais de um comerciante real (QA-04) ou implantação fora do ambiente local |
| Migração versionada (DbUp) no lugar dos scripts de inicialização | Primeira mudança de esquema depois da entrega |
| Saldo acumulado de fechamento, além do resultado do dia | Resposta da QA-01 indicando acumulado |
| Batimento periódico do despachante, para o Consolidado distinguir "sem lançamentos" de "outbox parada" | Requisito de detectar defasagem com o broker fora |

---

## Licença

[MIT](LICENSE).
