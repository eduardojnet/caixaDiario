# Diagramas de arquitetura

Modelo C4 do CaixaDiario em [Mermaid](https://mermaid.js.org/), renderizado nativamente pelo GitHub. Texto versionado: o diagrama muda no mesmo commit que o código, e a mudança aparece no diff.

| Nível | Diagrama | Público | Pergunta que responde |
|---|---|---|---|
| C1 | [Contexto](c1-contexto.md) | Negócio | Quem usa o sistema e com quem ele conversa? |
| C2 | [Contêineres](c2-conteineres.md) | TI | Que processos e armazenamentos compõem o sistema, e por que o registro sobrevive à queda do consolidado? |
| C3 | [Componentes](c3-componentes.md) | Engenharia | Como cada API se organiza por dentro, e onde cada regra vive? |
| Sequência | [Registro, despacho, projeção e consulta](sequencia-registro-e-projecao.md) | Engenharia | Em que ordem as coisas acontecem, e onde está cada garantia? |

## Implementado ou especificado

Cada diagrama mostra a **arquitetura-alvo** e declara o **estado atual** de cada elemento. Desenhar como existente o que ainda não existe seria apresentar o especificado como implementado.

| Convenção | Significado |
|---|---|
| Caixa azul, borda contínua | Implementado e coberto por teste |
| Caixa branca, borda tracejada | Especificado, não implementado |
| Caixa cinza | Sistema ou pessoa externa |
| Seta contínua | Interação implementada |
| Seta tracejada | Interação especificada, não implementada |

Cada diagrama traz, abaixo do desenho, uma tabela com o estado de cada elemento. A fonte do estado é o [ESTADO.md](../ESTADO.md) §4 e §5; havendo divergência, vale o `ESTADO.md`, e o diagrama está desatualizado.
