# C1: Contexto

Quem usa o CaixaDiario e com quem ele conversa. Público: negócio. Sem infraestrutura. Convenções em [README](README.md).

```mermaid
flowchart TB
    comerciante["Comerciante<br/>[Pessoa]<br/>Registra entradas e saídas do caixa<br/>e confere o saldo do dia"]:::externo
    contador["Contador<br/>[Pessoa]<br/>Confere o caixa para conciliação"]:::externo

    caixa["CaixaDiario<br/>[Sistema]<br/>Registra lançamentos de crédito e débito<br/>e entrega o saldo diário consolidado"]:::impl

    maquininha["Maquininha de cartão<br/>[Sistema externo]<br/>Vendas por cartão e Pix"]:::externoesp
    banco["Banco do comerciante<br/>[Sistema externo]<br/>Extrato da conta"]:::externoesp

    comerciante -->|"registra lançamentos,<br/>consulta o saldo do dia"| caixa
    contador -->|"consulta lançamentos e saldo do dia"| caixa
    maquininha -.->|"envia vendas recebidas"| caixa
    caixa -.->|"concilia com o extrato"| banco

    classDef externo fill:#999999,stroke:#6b6b6b,color:#fff
    classDef externoesp fill:#ffffff,stroke:#6b6b6b,color:#333,stroke-dasharray:5 5
    classDef impl fill:#1168bd,stroke:#0b4884,color:#fff
```

## Estado de cada elemento

| Elemento | Estado | Observação |
|---|---|---|
| Comerciante, contador | Pessoas | Sem autenticação no ambiente local (QA-04) |
| CaixaDiario | Ver [C2](c2-conteineres.md) | |
| Maquininha de cartão | Especificado, fora do escopo | Capacidade C4.1; o enunciado não exige integração externa |
| Banco do comerciante | Especificado, fora do escopo | Capacidade C4.2 |

## O que o diagrama diz ao negócio

O comerciante lança cada movimento de dinheiro e consulta o resultado do dia. As integrações com maquininha e banco reduziriam digitação e automatizariam a conferência, mas não são necessárias para o problema do enunciado; ficam como evolução.
