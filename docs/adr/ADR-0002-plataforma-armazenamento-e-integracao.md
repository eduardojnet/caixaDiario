# ADR-0002: Adotar .NET 10, PostgreSQL 17 por serviço, Dapper e outbox com RabbitMQ

- **Status:** Proposto
- **Data:** 2026-10-12
- **Decisor:** Eduardo J. G. do Carmo (Arquiteto de Soluções)
- **Consultados:** nenhum além do enunciado
- **Requisitos dirigentes:** RNF-01, RNF-02, RNF-04, RNF-06, RNF-07, RE-02, RE-03, RE-04, RE-05

## Contexto e problema

O [ADR-0001](ADR-0001-estilo-arquitetural.md) propõe dois serviços com integração assíncrona e um banco por serviço. Falta decidir com o que construir: plataforma, banco, acesso a dados, transporte do evento entre os serviços e ferramentas de teste. A decisão do transporte é a que mais pesa, porque é por ele que uma falha poderia voltar do consolidado ao registro.

## Critérios de decisão

Fixados em 2026-10-12, antes da comparação, em ordem de peso:

1. **Não cria caminho de falha ou de carga do consolidado para o registro** (RNF-01).
2. **Garante as invariantes do registro no próprio banco**, por constraint e privilégio, não por disciplina de código (RNF-06, RNF-07).
3. **Domínio do autor**, para caber no prazo (RE-02, RE-05).
4. **Roda localmente em contêiner**, sem licença paga e sem conta em nuvem (RE-04).
5. **Testável contra o componente real**, não contra simulação (RE-03).

## Opções consideradas e decisão, por tema

### 1. Plataforma: .NET 10 LTS com ASP.NET Core minimal API

**A decisão proposta é .NET 10 LTS**, com minimal API e `BackgroundService` para despacho e projeção.

- Atende os critérios 3 e 4: é a linguagem dominada pelo autor, suporte de longo prazo e imagem oficial de contêiner.
- Minimal API reduz cerimônia para quatro rotas; `BackgroundService` dá despachante e consumidor no mesmo processo do serviço, sem um terceiro executável.
- **Rejeitada: Java com Spring Boot.** Ecossistema equivalente para o problema; perde no critério 3. *Voltaria* se o time que mantém a solução for Java.
- **Rejeitada: Node.js.** Bom para I/O, mas o tratamento de valor monetário exige biblioteca de decimal e o autor tem menos domínio. *Voltaria* em time Node com o resto da plataforma em JavaScript.

### 2. Armazenamento: PostgreSQL 17, um banco por serviço

**A decisão proposta é PostgreSQL 17, com uma instância para Lançamentos e outra para Consolidado.**

- Critério 2: chave única garante idempotência (RNF-07); privilégio por papel garante imutabilidade (RNF-06: o papel da aplicação recebe só `SELECT` e `INSERT` no registro); `numeric(14,2)` representa valor monetário sem erro de arredondamento.
- Critério 1: um banco por serviço permite derrubar o consolidado **e o banco dele** sem tocar o banco do registro. Um banco compartilhado seria ponto de falha comum.
- **Rejeitada: um banco compartilhado com esquemas separados.** Menos uma peça, mas carga ou falha do consolidado atinge o banco crítico. *Voltaria* se o custo de duas instâncias fosse proibitivo e o consolidado fosse movido para cache sem banco.
- **Rejeitada: armazenamento chave-valor (Redis) para o modelo de leitura.** Consulta por chave seria ainda mais barata, mas acrescenta tecnologia sem gargalo medido e perde a aplicação idempotente transacional (inserir em `lancamentos_aplicados` e somar em `saldo_diario` na mesma transação). *Voltaria* se o teste de carga mostrar o banco do consolidado como gargalo.
- **Rejeitada: banco de documentos.** Sem ganho para registro tabular com invariantes de unicidade. *Voltaria* se lançamentos ganhassem estrutura variável por tipo de comerciante.

### 3. Acesso a dados: Dapper com SQL explícito

**A decisão proposta é Dapper sobre Npgsql.**

- Critério 2: as invariantes estão no banco; o SQL explícito deixa visível o `INSERT` que dispara a chave única e a transação que une lançamento e outbox. Um ORM esconderia exatamente o que precisa ser revisado.
- **Rejeitada: Entity Framework Core.** Produtivo para CRUD; aqui gera abstração sobre quatro comandos e induz a rastreamento de alteração num modelo que não tem alteração. *Voltaria* com modelo de domínio rico, muitas entidades e consultas compostas.

### 4. Transporte: outbox transacional com RabbitMQ

**A decisão proposta é outbox transacional no banco de Lançamentos, despachante publicando em RabbitMQ, e projetor no Consolidado consumindo de uma fila durável.**

| Opção | Critério 1: caminho de falha ou carga para o registro | Outros efeitos |
|---|---|---|
| **A. Outbox + RabbitMQ** | Nenhum. Lançamentos fala só com o próprio banco e com o broker. Broker fora: a outbox acumula, nada se perde, o registro segue. Consolidado fora: a fila retém | Uma peça a mais; entrega ao menos uma vez exige consumidor idempotente |
| B. Consolidado lê um feed HTTP de Lançamentos | Existe: o projetor em retentativa gera carga no registro; o registro passa a servir leitura para outro serviço | Sem broker. Cursor por sequência perde linhas com commit fora de ordem, exigindo janela de segurança |
| C. Consolidado lê o banco de Lançamentos | Existe e é direto: carga ou bloqueio do consolidado atinge o banco crítico | Nenhum código de integração; acoplamento por esquema |

- Critério 1 decide: só a opção A não cria caminho do consolidado para o registro.
- Critério 2: lançamento e evento são gravados na mesma transação. Não há lançamento sem evento nem evento sem lançamento. O despachante publica depois, com retentativa, e marca o evento como publicado.
- **Por que RabbitMQ e não Kafka.** 50 req/s de leitura e um volume de escrita menor não pedem log particionado nem retenção longa para reprocessamento. RabbitMQ entrega fila durável com confirmação de publicação e de consumo, numa imagem pequena. *Kafka voltaria* com múltiplos consumidores que precisem reler o histórico, ou volume de eventos que exija partição.
- **Por que não um broker gerenciado de nuvem.** Viola o critério 4 no ambiente local. *Voltaria* na implantação em nuvem, mantido o contrato do evento.
- **Gatilho de reabertura da opção B:** ambiente onde operar um broker seja proibido ou mais caro que o risco de carga cruzada no registro.
- **Opção C rejeitada sem gatilho:** fere diretamente o RNF-01.

### 5. Testes: xUnit v3, Testcontainers, NetArchTest e k6

**A decisão proposta é:**

- **Testcontainers** com PostgreSQL e RabbitMQ reais nos testes de integração. Critério 5: um repositório em memória passa na implementação ingênua de idempotência e imutabilidade; só o banco real recusa o `UPDATE` pelo privilégio e a duplicata pela chave única. *Rejeitado: banco em memória ou SQLite*, que não reproduzem papéis, privilégios nem a semântica de `numeric`. *Voltaria* para testes de unidade sem invariante de persistência, que já não usam banco.
- **NetArchTest** para provar que Lançamentos não referencia Consolidado. *Rejeitado: revisão manual*, que não falha o build. *Voltaria* nunca como substituto; só como complemento.
- **k6** para o RNF-02, com taxa de chegada constante. Executado em contêiner, sem instalação local. *Rejeitado: NBomber*, que exigiria um projeto .NET a mais só para carga; *voltaria* se o teste de carga precisasse reaproveitar código do domínio. *Rejeitado: JMeter*, mais pesado para um cenário de uma rota.

Versões dos pacotes já verificadas em outro projeto do autor são reaproveitadas; pacotes novos (cliente RabbitMQ, imagem do k6) ficam `[NVI]` até o restore e a execução passarem.

## Consequências

**Positivas**

- Invariantes de idempotência e imutabilidade garantidas pelo banco, provadas contra o banco real.
- Queda do broker ou do consolidado não afeta o registro.
- Todo o ambiente sobe com Docker Compose, sem licença e sem conta em nuvem.

**Negativas**

- Cinco contêineres no ambiente local (duas APIs, dois bancos, broker) contra dois de um monolito.
- Outbox exige um despachante com retentativa e monitoramento de tamanho; outbox crescendo sem parar indica broker fora.
- Consumidor idempotente é obrigatório: a entrega é ao menos uma vez, não exatamente uma vez.
- SQL explícito é mais texto a manter que um ORM.
- Sem migração versionada neste prazo: os esquemas são aplicados por script de inicialização do contêiner. Mudança de esquema após a entrega exige adotar um migrador.

**Neutras**

- Testes de integração exigem Docker na máquina de quem os executa.

### Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Outbox cresce sem limite com o broker fora por muito tempo | Monitorar tamanho da outbox (Arquitetura Alvo, observabilidade); o banco do registro comporta dias de acúmulo na carga declarada `[NVI]` |
| Evento publicado e não marcado como publicado (queda entre publicar e marcar) | Reenvio é esperado; o consumidor descarta duplicata pela chave única |
| Ordem de chegada diferente da ordem de registro | A projeção é soma comutativa por dia; a ordem não altera o resultado |

## Validação

- Teste de integração: broker fora, `POST` responde `201` e a outbox acumula; broker de volta, a outbox esvazia.
- Teste contra PostgreSQL real: `UPDATE` e `DELETE` pelo papel da aplicação são recusados.
- Teste: mesma chave de idempotência e mesmo corpo geram um lançamento só.

## Gatilho de revisão

- Volume de eventos que exija partição ou reprocessamento histórico por múltiplos consumidores (reabre Kafka).
- Proibição de operar broker no ambiente-alvo (reabre a opção B).
- Primeira mudança de esquema depois da entrega (adotar migrador versionado).
