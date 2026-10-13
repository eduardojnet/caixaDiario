# ADR-0004: Proteger a API com OAuth2 client credentials e o broker com credencial por serviço e TLS

- **Status:** Aceito em 2026-10-12 (Eduardo determinou a execução do cartão 37; mecanismos decididos neste ADR)
- **Data:** 2026-10-12
- **Decisor:** Eduardo J. G. do Carmo (Arquiteto de Soluções)
- **Consultados:** nenhum
- **Requisitos dirigentes:** RNF-01, RNF-02, QA-04; Arquitetura Alvo §4.8

## Contexto e problema

Até aqui, qualquer cliente que alcançasse as portas registrava lançamentos e lia saldos, e os dois serviços usavam o mesmo usuário administrador do broker, em texto claro. Três riscos: lançamento forjado por quem não é o caixa; um defeito ou invasão no Consolidado publicando eventos falsos que somariam ao saldo; credenciais e eventos legíveis na rede. A QA-04 admite vários comerciantes, o que torna a autenticação obrigatória antes de qualquer uso real.

## Critérios de decisão

Fixados em 2026-10-12, antes das opções:

1. **Não cria dependência que derrube o registro** quando o Consolidado cai (RNF-01), e minimiza a dependência nova do servidor de autorização.
2. **Privilégio mínimo verificável por teste:** recusa sem token, recusa sem escopo, recusa de publicação pelo Consolidado.
3. **Padrão aberto**, sem amarrar a fornecedor.
4. **Roda localmente** no mesmo `docker compose`, sem segredo real no repositório.
5. **Não compromete o RNF-02:** 50 req/s com até 5% de falha, medido.

## Opções consideradas

Para a API: (A) OAuth2 client credentials com JWT validado localmente pela API; (B) chave de API estática por cliente; (C) mTLS entre cliente e API.

Para o broker: (D) um usuário por serviço com permissões por recurso, mais TLS; (E) usuário único com TLS; (F) manter usuário único sem TLS.

## Decisão

**A solução utiliza OAuth2 client credentials (opção A), com Keycloak como servidor de autorização local e validação do JWT nas APIs (emissor, audiência `caixadiario-api`, validade, assinatura e escopo); e, no broker, um usuário por serviço com privilégio mínimo e TLS obrigatório na porta 5671 (opção D).**

**Escopos:**

| Escopo | Operação | Cliente local `caixa-loja` | Cliente local `contador` |
|---|---|---|---|
| `lancamentos.escrita` | `POST` em Lancamentos | Sim | Não |
| `lancamentos.leitura` | `GET` da lista do dia | Sim | Sim |
| `consolidado.leitura` | `GET` do consolidado | Sim | Sim |

`/health/*` permanece sem autenticação, para o orquestrador.

**Permissões no broker** (`infra/rabbitmq/permissoes.sh`, o mesmo script no compose e nos testes):

| Usuário | Declarar | Publicar e ligar | Ler e consumir |
|---|---|---|---|
| `lancamentos` | só a exchange `caixadiario.lancamentos` | só a exchange `caixadiario.lancamentos` | nada |
| `consolidado` | a exchange e as filas `consolidado.*` | só as filas `consolidado.*` (ligação) | a exchange (ligação) e as filas `consolidado.*` |

**Justificativa pelos critérios:**

- **Critério 1:** a API valida o token localmente, com as chaves públicas do servidor de autorização em cache; não há chamada ao Keycloak por requisição. Verificado: com o Keycloak parado, um token válido continua aceito (`201`). O Consolidado não entra no caminho de autenticação de Lancamentos.
- **Critério 2:** escopo por operação e permissões por recurso no broker; os quatro testes de `SegurancaDeIntegracaoTests` provam as recusas, e o teste ponta a ponta prova que as permissões mínimas bastam.
- **Critério 3:** OAuth2, JWT e OpenID Connect; trocar o Keycloak por outro servidor é trocar configuração.
- **Critério 4:** Keycloak em modo de desenvolvimento no compose, realm importado de `infra/keycloak/`; segredos locais com padrão declarado e substituíveis por variável de ambiente; certificados do broker gerados no primeiro `up`, nunca versionados.
- **Critério 5:** medido com autenticação: 3001 requisições a 50 req/s, 0,00% de falha, p95 de 3,3 ms (era 1,56 ms sem autenticação).

## Consequências

**Positivas**

- Só clientes registrados registram lançamentos; o contador lê e não escreve.
- O Consolidado, mesmo comprometido, não consegue publicar eventos na exchange de Lancamentos.
- Credenciais e eventos do broker trafegam cifrados.

**Negativas**

- **Dependência nova do servidor de autorização:** se Lancamentos reiniciar enquanto o Keycloak está fora, não obtém as chaves e recusa tokens (`401`) até o Keycloak voltar. Com o processo já no ar, o cache cobre a queda. Em produção, o servidor de autorização precisa de alta disponibilidade, ou as chaves podem ser fornecidas por configuração.
- Dois contêineres a mais no ambiente local (Keycloak e o gerador de certificados, que termina após gerar).
- Todo exemplo do README passa a obter um token antes da chamada.
- O usuário `consolidado` pode declarar a exchange, para não depender da ordem de subida; com isso, também poderia apagá-la. Restringir exigiria a exchange pré-declarada pela infraestrutura.
- O realm local usa HTTP e segredos padrão conhecidos: adequado só ao ambiente local, declarado.

**Neutras**

- Os testes de integração emitem tokens com uma chave de teste em vez de subir o Keycloak; a validação na API é a mesma. Os testes usam o broker sem TLS; o TLS é verificado no compose.

## Análise das opções rejeitadas

**Chave de API estática (B).** Mais simples, mas sem expiração, sem escopo padronizado e com rotação manual por cliente. *Voltaria* para integração com um único parceiro, sem servidor de autorização disponível.

**mTLS na API (C).** Forte, mas distribuir e rotacionar certificado por cliente (caixa, maquininha) é custo alto para o perfil do comerciante. *Voltaria* para integração entre serviços internos numa malha com identidade de serviço.

**Usuário único com TLS (E).** Cifra o tráfego, mas o Consolidado continuaria podendo publicar eventos falsos. *Voltaria* nunca como alvo; só como passo intermediário.

**Sem mudança (F).** Rejeitada: é o estado que motivou o cartão.

## Validação

- `SegurancaDeIntegracaoTests`: `401` sem token, com assinatura, emissor ou audiência inválidos; `403` sem escopo; `201` com escopo; `/health/ready` aberto; Consolidado `401` e `403`; usuário `consolidado` recebe `ACCESS_REFUSED` (403) ao publicar na exchange; usuário `lancamentos` recebe `ACCESS_REFUSED` ao declarar a fila.
- `FluxoCompletoTests` com as credenciais por serviço.
- No compose: porta 5672 fechada; `openssl s_client` na 5671 valida o certificado contra a CA local.

## Gatilho de revisão

- Implantação fora do ambiente local: HTTPS no servidor de autorização (`ExigirHttps`), segredos em cofre, alta disponibilidade do servidor de autorização.
- Autorização por caixa (um comerciante não pode ler o caixa de outro), quando QA-04 for respondida com vários comerciantes reais.
