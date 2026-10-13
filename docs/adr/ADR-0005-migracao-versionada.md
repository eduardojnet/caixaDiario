# ADR-0005: Adotar migração versionada com DbUp, um migrador por serviço

- **Status:** Aceito em 2026-10-12 (Eduardo determinou a execução do cartão 39). Supera, no ADR-0002, a consequência "sem migração versionada neste prazo"
- **Data:** 2026-10-12
- **Decisor:** Eduardo J. G. do Carmo (Arquiteto de Soluções)
- **Consultados:** nenhum
- **Requisitos dirigentes:** RNF-06, RE-04; gatilho de revisão do ADR-0002

## Contexto e problema

O [ADR-0002](ADR-0002-plataforma-armazenamento-e-integracao.md) aceitou aplicar os esquemas pelo `docker-entrypoint-initdb.d` do PostgreSQL e declarou o gatilho: "primeira mudança de esquema depois da entrega". O gatilho foi cumprido pelo [ADR-0003](ADR-0003-observabilidade.md), que acrescentou `outbox.traceparent`. Com o `initdb`, um script novo só roda em volume vazio: quem já tinha dados precisava apagar o volume para receber a coluna.

## Critérios de decisão

Fixados em 2026-10-12, antes das opções:

1. **Mudança de esquema sem recriar o volume**, e sem reaplicar o que já foi aplicado.
2. **Um migrador por serviço**, sem acoplar os bancos de Lancamentos e Consolidado (ADR-0001).
3. **Privilégio mínimo preservado:** a credencial administrativa não fica com a API (RNF-06).
4. **Os testes usam o mesmo caminho do compose** (regra 8).
5. **Domínio do autor e proporcionalidade:** pouco código novo.

## Opções consideradas

1. DbUp, um console por serviço, executado antes da API.
2. Migrations do Entity Framework Core.
3. Flyway em contêiner.
4. Manter o `initdb`.

## Decisão

**A solução utiliza DbUp 7.0.1 com um console por serviço (`CaixaDiario.Lancamentos.Migracoes` e `CaixaDiario.Consolidado.Migracoes`), que aplica os scripts de `db/<serviço>/` ainda não registrados no diário `versoes_do_esquema`, define a senha do papel da aplicação e termina. No compose, cada API só sobe depois que o seu migrador termina com sucesso.**

- **Critério 1:** o DbUp registra cada script aplicado; a execução seguinte só aplica o que falta. Verificado no compose (segunda subida: "Nenhum script novo") e por teste (script novo aplicado sozinho sobre banco existente).
- **Critério 2:** dois migradores, dois diários, dois bancos; o de Lancamentos não referencia nada do Consolidado (teste de arquitetura).
- **Critério 3:** o migrador é o único processo com a credencial administrativa; a API conecta com o papel que só lê e insere. A senha desse papel vem de variável de ambiente e é redefinida a cada execução, o que permite trocá-la sem recriar o banco; substitui o `0002_senha_aplicacao.sh`.
- **Critério 4:** a fixture dos testes de integração executa o mesmo `Migrador`, com os mesmos scripts.
- **Critério 5:** mesmo padrão já implementado pelo autor no PacioliBank; cerca de 60 linhas por serviço.

## Consequências

**Positivas**

- Mudança de esquema é um arquivo novo em `db/<serviço>/`, aplicado na próxima subida, sem perder dados.
- Diário consultável: `SELECT * FROM versoes_do_esquema` mostra o que foi aplicado e quando.

**Negativas**

- Dois contêineres a mais no compose, que terminam após aplicar; duas imagens a construir.
- O código do migrador é duplicado entre os serviços, por decisão (sem assembly compartilhado, ADR-0002).
- Volume criado pela versão anterior (pelo `initdb`, sem diário) é incompatível: o migrador tentaria recriar as tabelas. Quem tinha o ambiente de antes precisa de um `docker compose down -v` uma única vez.
- Não há migração para trás: desfazer é um script novo.

## Análise das opções rejeitadas

**EF Core Migrations.** Exigiria o EF Core, rejeitado no ADR-0002 em favor do Dapper. *Voltaria* se o acesso a dados migrasse para EF Core.

**Flyway.** Equivalente em função, mas traz a JVM e outra linguagem de configuração para uma solução .NET. *Voltaria* numa organização que já padronize Flyway entre equipes.

**Manter o `initdb`.** Rejeitada: é exatamente o que o gatilho do ADR-0002 mandou rever.

## Validação

- `MigracaoTests`: cada migrador aplica uma vez e, na segunda execução, nada; script novo aplicado sozinho sobre banco existente; senha do papel redefinida a cada execução, inclusive com aspas.
- Teste de arquitetura: `CaixaDiario.Lancamentos.Migracoes` não referencia `CaixaDiario.Consolidado`.
- Compose: duas subidas sobre o mesmo volume; a segunda não aplica nada.

## Gatilho de revisão

- Necessidade de migração com dados volumosos ou sem parada (exigiria scripts em etapas e compatibilidade entre versões da API).
- Mais de um time alterando o mesmo esquema (exigiria convenção de numeração mais rígida).
