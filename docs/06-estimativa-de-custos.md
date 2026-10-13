# Estimativa de Custos

**Público:** negócio (seção 1) e TI (seções 2 a 6).
**Data dos preços:** 2026-10-12, consultados na API pública de preços da AWS (detalhe na seção 5).
**Estado:** **especificado.** Nada está implantado em nuvem; o ambiente executável deste repositório é local (Docker Compose).

## 1. Resumo

Rodar o CaixaDiario em nuvem, na carga declarada pelo enunciado, custa cerca de **US$ 196 por mês** na topologia recomendada. Ela mantém o registro e o consolidado em bancos e processos separados, que é o que garante que a queda do relatório não para o caixa. Uma versão mínima, tudo numa única máquina virtual, custa cerca de **US$ 52 por mês**, mas uma falha dessa máquina para tudo de uma vez. **Licenças: custo zero**; todas as dependências são de código aberto.

Valores em dólar, sem impostos e sem câmbio. O valor final em reais depende do câmbio do dia e da tributação da fatura no Brasil, e deverá ser refinado na contratação `[NVI]`.

## 2. Premissas

| ID | Premissa | Impacto se não se confirmar |
|---|---|---|
| PC-01 | Provedor: AWS, região São Paulo (`sa-east-1`). Escolhida por oferecer RabbitMQ gerenciado (Amazon MQ), o que mantém o [ADR-0002](adr/ADR-0002-plataforma-armazenamento-e-integracao.md) sem trocar de broker | Em outro provedor sem RabbitMQ gerenciado, o broker roda em contêiner próprio ou o transporte troca de produto, mantido o contrato v1 (gatilho já previsto no ADR-0002) |
| PC-02 | Carga do enunciado: pico de 50 req/s no Consolidado; registro com volume menor e não declarado (PR-01) | Carga sustentada maior muda o tamanho das instâncias, não a topologia |
| PC-03 | Preço sob demanda, 730 horas por mês, sem compromisso de uso nem desconto | Compromisso de 1 ou 3 anos reduz instâncias e bancos; não calculado aqui |
| PC-04 | 5 GB de log por mês; tráfego de saída dentro da franquia gratuita do provedor | Log mais verboso ou muito tráfego de saída aumentam a linha de log e acrescentam transferência |
| PC-05 | Uma zona de disponibilidade para bancos e broker; sem réplica | Exigência de alta disponibilidade multizona aproximadamente dobra bancos e broker (seção 4) |

## 3. Topologia recomendada (opção A): serviços gerenciados, isolamento preservado

Mesma forma do [C2](diagrams/c2-conteineres.md): duas APIs, dois bancos, um broker.

| Componente | Recurso | Quantidade | Preço unitário (USD) | Mensal (USD) |
|---|---|---|---|---|
| APIs Lancamentos e Consolidado | AWS Fargate ARM, vCPU | 2 tarefas × 0,5 vCPU × 730 h | 0,0557 por vCPU-hora | 40,66 |
| | AWS Fargate ARM, memória | 2 tarefas × 1 GB × 730 h | 0,00612 por GB-hora | 8,94 |
| Entrada HTTP | Application Load Balancer, hora | 730 h | 0,034 por hora | 24,82 |
| | Application Load Balancer, capacidade | 1 LCU constante (teto: 50 req/s cabem em 1 LCU) | 0,011 por LCU-hora | 8,03 |
| Banco de lançamentos e banco do consolidado | RDS PostgreSQL `db.t4g.micro` Single-AZ | 2 × 730 h | 0,034 por hora | 49,64 |
| | RDS armazenamento gp3 | 2 × 20 GB | 0,219 por GB-mês | 8,76 |
| Broker | Amazon MQ RabbitMQ `mq.t3.micro`, instância única | 730 h | 0,04368 por hora | 31,89 |
| | Amazon MQ armazenamento EBS | 20 GB `[NVI]` (tamanho fixo do tipo de instância, não conferido) | 0,19 por GB-mês | 3,80 |
| Rede | IPv4 público | 4 endereços (2 do balanceador, 2 das tarefas) × 730 h | 0,005 por hora | 14,60 |
| Observabilidade | CloudWatch Logs, ingestão | 5 GB | 0,90 por GB | 4,50 |
| **Total** | | | | **≈ 195,63** |

**Decisões de dimensionamento:**

- **Dois bancos `db.t4g.micro`, não um maior.** Um banco por serviço é o que impede a carga do consolidado de atingir o banco crítico (ADR-0002). Custa US$ 24,82 a mais que um banco compartilhado; é o preço do RNF-01 na camada de dados.
- **Tarefas em sub-rede pública com IP próprio, entrada só pelo balanceador.** Evita um NAT Gateway, que custaria mais que as duas tarefas juntas. O custo é expor um IP por tarefa, mitigado por grupo de segurança que só aceita tráfego do balanceador. Gatilho de revisão: exigência de rede privada sem IP público.
- **Sem réplicas.** O teste de carga mediu 0,00% de falha a 50 req/s com uma instância local de cada serviço (RNF-02); réplicas não são justificadas pela carga declarada.

**Não incluído:** registro das imagens de contêiner (ECR, poucos centavos por GB), DNS e certificado, cópias de segurança além da franquia do RDS, suporte pago da AWS.

## 4. Alternativas

| Opção | Mensal (USD) | O que ganha | O que perde | Gatilho para escolher |
|---|---|---|---|---|
| **A. Gerenciada, Single-AZ** (recomendada) | ≈ 195,63 | Isolamento de processo e de banco; atualização e cópia de segurança dos bancos e do broker pelo provedor | Custo cerca de quatro vezes o da opção B | |
| B. Uma VM com Docker Compose | ≈ 51,84 | Custo mínimo; mesmo `docker-compose.yml` deste repositório | Falha da máquina derruba registro e consolidado juntos; o RNF-01 continua valendo para falha de processo, não de máquina. Atualização, cópia de segurança e monitoramento ficam manuais | Ambiente de demonstração ou piloto com um caixa, onde parada da máquina é aceitável |
| C. A com alta disponibilidade multizona | ≈ 2 × (bancos + broker) acima de A, não calculado | Sobrevive à perda de uma zona | Custo sem carga que o justifique | SLO de disponibilidade do registro acima do que uma zona entrega, definido pelo negócio |

**Opção B detalhada:** EC2 `t4g.medium` (4 GB, cabe os cinco contêineres) 730 h × US$ 0,0536 = 39,13; EBS gp3 30 GB × US$ 0,152 = 4,56; 1 IPv4 × 730 h × US$ 0,005 = 3,65; logs 5 GB × US$ 0,90 = 4,50.

## 5. Fontes

Todos os preços foram lidos em 2026-10-12, da AWS Price List Bulk API (`https://pricing.us-east-1.amazonaws.com/offers/v1.0/aws/<serviço>/<versão>/sa-east-1/index.json`), termo `OnDemand`, em USD.

| Serviço | Versão do arquivo (data de publicação pela AWS) | Preços usados |
|---|---|---|
| AmazonECS (Fargate) | `20260911124425` (2026-09-11) | `SAE1-Fargate-ARM-vCPU-Hours:perCPU` 0,0557; `SAE1-Fargate-ARM-GB-Hours` 0,00612 |
| AWSELB | `20260911124544` (2026-09-11) | `SAE1-LoadBalancerUsage` (Application) 0,034; `SAE1-LCUUsage` (Application) 0,011 |
| AmazonRDS | `20261006224050` (2026-10-06) | `db.t4g.micro` PostgreSQL Single-AZ 0,034; `SAE1-RDS:GP3-Storage` PostgreSQL 0,219 |
| AmazonMQ | `20260911124456` (2026-09-11) | `SAE1-RabbitMQ-Single-InstanceUsage:mq.t3.micro` 0,04368; `SAE1-TimedStorage-RabbitMQ-ByteHrs` 0,19 |
| AmazonVPC | `20260917190528` (2026-09-17) | `SAE1-PublicIPv4:InUseAddress` 0,005 |
| AmazonCloudWatch | `20261007115540` (2026-10-07) | `SAE1-DataProcessing-Bytes` (Ingested Logs) 0,90 |
| AmazonEC2 | `20261012193925` (2026-10-12) | `SAE1-BoxUsage:t4g.medium` Linux 0,0536; `EBS:VolumeUsage.gp3` 0,152 |

## 6. Licenças

Nenhuma licença paga. Licenças conferidas em 2026-10-12 no metadado do pacote (nuget.org) ou no repositório oficial do projeto.

| Componente | Licença | Uso nesta solução |
|---|---|---|
| .NET (runtime e ASP.NET Core) | MIT | Execução das APIs |
| PostgreSQL | PostgreSQL License | Bancos |
| RabbitMQ | MPL 2.0 | Broker |
| Npgsql | PostgreSQL License | Driver do banco |
| Dapper | Apache 2.0 | Acesso a dados |
| RabbitMQ.Client | Apache 2.0 ou MPL 2.0 | Cliente do broker |
| xunit.v3 | Apache 2.0 | Testes |
| Testcontainers | MIT | Testes de integração |
| NetArchTest.Rules | MIT | Teste de arquitetura |
| coverlet.MTP | MIT | Cobertura |
| Microsoft.AspNetCore.Mvc.Testing | MIT | Testes de integração |
| Stryker.NET | Apache 2.0 | Teste de mutação |
| k6 | AGPL 3.0 | Ferramenta de teste de carga, executada em contêiner e não distribuída com a solução; a AGPL não alcança o código do CaixaDiario |

Em serviço gerenciado, a licença do PostgreSQL e do RabbitMQ está embutida no preço por hora do provedor; não há cobrança à parte.
