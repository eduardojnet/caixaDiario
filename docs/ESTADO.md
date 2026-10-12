# Estado do Projeto

**Documento vivo.** Atualizado ao fim de cada bloco de trabalho. Descreve o que existe, o que falta e o que está decidido, sem otimismo.

**Última atualização:** 2026-10-12 (Bloco 0 concluído)
**Build:** convenções definidas (`Directory.Build.props`, `global.json`); nenhum projeto ainda
**Testes:** inexistentes

---

## 1. Identificação

| | |
|---|---|
| Produto | **CaixaDiario** |
| Repositório | https://github.com/eduardojnet/caixaDiario (público) |
| Propósito | Desafio técnico de Arquiteto de Soluções |
| Domínio | Fluxo de caixa diário de um comerciante: lançamentos de débito e crédito e saldo diário consolidado. Não é conta corrente bancária |
| Teto de tempo | 5 horas de trabalho efetivo (300 min) |
| Plataforma prevista | .NET 10, PostgreSQL 17, broker a decidir no ADR-0002 |
| Decisor | Eduardo J. G. do Carmo |

---

## 2. Requisitos obrigatórios do enunciado

| # | Requisito | Estado |
|---|---|---|
| O1 | Mapeamento de domínios funcionais e capacidades de negócio | Pendente (Bloco 1) |
| O2 | Refinamento de requisitos funcionais e não funcionais | Pendente (Bloco 1) |
| O3 | Desenho da solução completo (Arquitetura Alvo) | Pendente (Bloco 3) |
| O4 | Justificativa de ferramentas, tecnologias e tipo de arquitetura | Pendente (Bloco 2) |
| O5 | Linguagem dominada | Previsto: C# / .NET 10 |
| O6 | Testes | Pendente (Bloco 4) |
| O7 | README: como funciona e como rodar localmente | Pendente (Bloco 5) |
| O8 | Repositório público no GitHub | Remoto criado e vazio; repositório local inicializado com `origin`; primeiro push só após o G2 |
| O9 | Toda documentação no repositório | Pendente (Blocos 1 a 5) |

> O enunciado descarta o teste se algum requisito obrigatório não for minimamente atendido. Nenhum item desta tabela pode terminar como "Pendente".

---

## 3. Decisões e portões

| Item | Estado |
|---|---|
| ADR-0001 estilo arquitetural | Não redigido |
| ADR-0002 plataforma, armazenamento e integração | Não redigido |
| G1 pré-voo | Não acionado: as cinco verificações responderam (2026-10-12 19:55) |
| G2 aprovação dos ADRs e autorização de push | Pendente |

---

## 4. Implementado

- Esqueleto do repositório: `git init`, remoto `origin`, `Directory.Build.props` (.NET 10, `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, `NuGetAudit`), `global.json` (Microsoft Testing Platform), `docs/adr/template.md` (MADR em português), `.editorconfig`, `.gitignore`.

---

## 5. Especificado, não implementado

Nada ainda.

---

## 6. Questões abertas e lacunas

| ID | Questão | Conduta provisória | Origem |
|---|---|---|---|
| L-01 | Números do PacioliBank no texto de citação divergiam do repositório (95% e 87% declarados; 94,11% e 86,96% medidos) | Texto corrigido no CLAUDE.md §5.3; reconferir no Bloco 5 | Verificação de 2026-10-12 |
| L-02 | Plano original não tinha ADR de tecnologia (requisito O4) | ADR-0002 incluído no Bloco 2 | Verificação de 2026-10-12 |

---

## 7. Fila de execução

| Bloco | Teto (min) | Início | Fim | Gasto | Estado | Critério de conclusão |
|---|---|---|---|---|---|---|
| 0. Pré-voo e repositório | 10 | 19:55 | 19:57 | 2 | Concluído | Ferramentas respondem; remoto configurado |
| 1. Fronteira e requisitos | 65 | | | | Pendente | Dois serviços nominais no mapa; RNF-01 e RNF-02 com meta e medição |
| 2. Decisões | 40 | | | | Pendente | Frase volume x isolamento no ADR-0001; G2 aprovado |
| 3. Arquitetura Alvo | 35 | | | | Pendente | C2 renderiza; sobrevivência de Lancamentos visível |
| 4. Código e testes | 80 | | | | Pendente | Compose sobe; POST 201; saldo confere; testes passam; POST 201 com Consolidado parado |
| 5. README e fechamento | 40 | | | | Pendente | Clone limpo sobe só com o README |
| Reserva | 30 | | | | Disponível | Consumida apenas pelo Bloco 4 ou por revisão final |
| **Total** | **300** | | | **2** | | |

---

## 8. Histórico

| Data | Evento |
|---|---|
| 2026-10-12 | Acessos verificados: repositório GitHub público e vazio com permissão de escrita; PacioliBank legível; pasta local com leitura e escrita |
| 2026-10-12 | Instruções de execução geradas (CLAUDE.md local, skill `/proximo`) e estado inicial registrado |
| 2026-10-12 | Bloco 0, pré-voo: `dotnet --list-sdks` → 10.0.401; `docker version` → cliente e servidor 29.8.2; `docker compose version` → v5.6.0; `git --version` → 2.43.0; `gh` ausente, acesso ao GitHub verificado por SSH (`ssh -T git@github.com` autentica `eduardojnet`) e `git ls-remote` HTTPS no remoto vazio retorna sem erro |
| 2026-10-12 | Bloco 0: remoto `origin` em HTTPS, conforme o plano; se o push HTTPS falhar por credencial após o G2, trocar para `git@github.com:eduardojnet/caixaDiario.git` (SSH verificado). Decisão reversível |
| 2026-10-12 | Bloco 0: `Directory.Build.props` e `template.md` adaptados do PacioliBank, com comentários e IDs de requisito daquele projeto removidos (regra 13); `global.json` idêntico |
| 2026-10-12 | Bloco 0: identidade git local do repositório igual à do PacioliBank (`Eduardo J. Net <eduardojnet@outlook.com>`), pois `user.name` global não está definido |
