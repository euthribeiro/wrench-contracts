# Wrench.Contracts

Contratos das mensagens da **Saga** da _Wrench Auto Repair_ (Tech Challenge — Fase 4).

O pacote é a única fronteira compartilhada entre os três microsserviços: nenhum deles lê o banco
do outro, então tudo que um serviço precisa saber viaja dentro da mensagem. Ter os contratos em
um pacote versionado evita que uma mudança de campo em um repositório quebre o consumidor do
outro sem ninguém perceber.

- **Pacote:** `Wrench.Contracts` (GitHub Packages)
- **Target:** `net10.0`
- **Política de versão:** SemVer com **mudanças apenas aditivas** na linha 1.x. Campo novo entra
  como opcional; renomear ou remover campo exige nova major e atualização combinada dos três
  repositórios.

## Mensagens

Toda mensagem implementa `ISagaMensagem` e carrega `OrdemServicoId` e `CorrelationId`.

### OS Service publica (tópico `wrench-os-events`)

| Tipo | Destino | Papel |
| ---- | ------- | ----- |
| `GerarOrcamento` | Billing | Diagnóstico concluído: monte o orçamento |
| `CancelarOrcamento` | Billing | **Compensação:** cancela orçamento e cobrança |
| `EstornarPagamento` | Billing | **Compensação:** devolve o valor pago |
| `IniciarExecucao` | Execução | Pagamento confirmado: entre na fila |
| `CancelarExecucao` | Execução | **Compensação:** saia da fila |

### OS Service consome (fila `wrench-os-saga`)

| Tipo | Origem | Efeito na Saga |
| ---- | ------ | -------------- |
| `OrcamentoCriado` | Billing | Aguarda a decisão do cliente |
| `OrcamentoFalhou` | Billing | Compensa: a OS volta para `EmDiagnostico` |
| `OrcamentoAprovado` | Billing | Aguarda o pagamento |
| `OrcamentoRecusado` | Billing | Compensa: a OS vai para `Cancelada` |
| `PagamentoAprovado` | Billing | Emite `IniciarExecucao` |
| `PagamentoFalhou` | Billing | Compensa: cancela orçamento, OS `Cancelada` |
| `PagamentoEstornado` | Billing | Encerra a compensação |
| `EstornoFalhou` | Billing | Encerra a compensação exigindo tratamento manual |
| `ExecucaoConcluida` | Execução | A OS vai para `Finalizada` |
| `ExecucaoFalhou` | Execução | Compensa: estorno + saída da fila, OS `Cancelada` |

## Transporte

O corpo da mensagem é o JSON do contrato (`JsonSerializerDefaults.Web`, portanto `camelCase`),
publicado em SNS com `raw_message_delivery = true`. Os metadados vão em _message attributes_,
cujos nomes estão em `AtributosMensagem`:

| Atributo | Conteúdo |
| -------- | -------- |
| `messageId` | Identificador da mensagem no outbox do publicador; base da idempotência do consumidor |
| `tipo` | Nome do contrato (`MensagemTipos`); é o que o filter policy da assinatura SNS avalia |
| `correlationId` | Correlação da ordem de serviço, propagada no header `X-Correlation-ID` |
| `traceparent` | Contexto W3C do trace, que mantém o rastro distribuído no New Relic |

`MensagemTipos.ComandosDoOrquestrador` e `MensagemTipos.EventosDoOrquestrador` listam os tipos de
cada direção e são as mesmas listas usadas nos filtros das assinaturas em
`infra-k8s/terraform/mensageria`.

## Como consumir

```xml
<PackageReference Include="Wrench.Contracts" Version="1.0.0" />
```

Com um `nuget.config` apontando para o feed do GitHub Packages:

```xml
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/euthribeiro/index.json" />
  </packageSources>
</configuration>
```

A autenticação usa o `GITHUB_TOKEN` do workflow (ou um PAT com `read:packages` em desenvolvimento).

## Como publicar

O workflow `publish.yml` publica no GitHub Packages quando uma tag `v*` é criada:

```bash
git tag v1.0.0
git push origin v1.0.0
```

A versão do pacote vem da tag; `Version` no `.csproj` é apenas o padrão local.

## Testes

```bash
dotnet test
```

Os testes travam o contrato: todo tipo listado em `MensagemTipos` precisa existir como mensagem,
comandos e eventos não se sobrepõem e a serialização preserva o conteúdo em `camelCase`.
