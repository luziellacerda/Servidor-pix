# R77 recebida: leitura e comparação com a produção — 08/10/2026

**SERVIDOR → APP.** Entrega recebida `6c52830f121ff5f3fae403c64a49ace11153cc0e`;
fonte executável do app `ba4fee556ec0010bbde1287746c68a2882cd1e39`,
atualização documental `04a58b77a4d4761e047cfef7ee7f8837a869e342`.
Esta revisão lê documentos, fontes e recibos. Não compila, instala ou ativa a R77.

## Estado confirmado

- Handoff, README, STATUS, contrato, perfis, vínculo do catálogo e limites dos
  testes lidos. Os 142 arquivos declarados em DELIVERY tiveram tamanho e SHA256
  conferidos; o manifesto é o 143º arquivo da publicação.
- Conferidos os 75 arquivos do manifesto C# e os hashes dos 23 recibos ligados
  ao pacote. Os resultados dos testes são recebidos do PCAPK; não foram
  executados novamente nesta leitura. Conferir o recibo desta revisão em
  `recovery-r77-20261008/RECIBO-LEITURA-HANDOFF.json`.
- A candidata C# parte de `ab192bf`, com quatro arquivos da base alterados e
  novos componentes v3. A candidata de observações `6f27` não foi incorporada;
  sua qualificação TLS pendente não foi resolvida por esta entrega.
- APK R77 `14450f3aa52ca2c795b50afba7e5a75c5bd4f71aa0007d2c43c6542051bdb737`:
  compilado/assinado no PCAPK, **não instalado conforme o recibo**. A última
  instalação comprovada dos dois celulares continua sendo R76.
- Produção conferida às 13:48 UTC: Station PID 1278094, NRestarts 0, v2,
  zero salas/conexões/pendências. A DLL continua identificada pelo recibo
  anterior como `ab192bf`/`815fc8bc`; não houve nova rehash privilegiada.
  Os dez engines existentes permanecem preservados.

## Pontos concretos de integração

| Requisito | Situação da entrega |
|---|---|
| Rotas e transporte | Candidata implementa `online/multiplayer/command`, `online/multiplayer/relay` e `station-stream.v3`; a produção atual não possui essa extensão |
| Identidades | Runtime R77 `351cee4540e9…`, core SNES `0a3ac7b4fa5d…`; precisam de vínculo exato nos perfis v3 |
| Autorização por jogo/modo | Um perfil piloto proposto, `approved:false`; zero perfis aprovados |
| Salas v2 na migração | Ativar v3 exige `MultiplayerLegacyCapacityGate`; novas salas antigas também passam a exigir perfil aprovado compatível |
| Contador de jogadores | Asset factual vazio; o catálogo precisa expor `contentSha256` para os vínculos revisados |
| Convites e códigos | Não implementados para v3; entrada da candidata ocorre pela lista de salas |

### Preservação das salas de duas pessoas

`StationOnlineRegistration.cs` exige o gate legado quando v3 é ativado.
`StationOnline.cs` aplica a classificação em create/join/start.
`StationMultiplayer.LegacyAllowed` exige conteúdo, engine, core e runtime
exatos, perfil aprovado `standard-2p-v1` e autorização para dois participantes.

**Ativar os flags com o arquivo proposto atual recusaria novas salas v2 e v3.**
O registro contém apenas um piloto não aprovado. Antes de uma migração, preparar
e qualificar também os perfis legados dos títulos que devem continuar disponíveis;
preservar dez engines, por si só, não conserva essa admissão.

O `EngineRegistryFile` existente aceita protocolos v2. Manter seus dez registros.
Os IDs/runtime/core de v3 devem ser vinculados no cadastro separado de perfis
multiplayer; inserir os registros `recoveryProtocol: station-stream.v3` do asset
Android diretamente no arquivo legado faria a validação de inicialização recusar
o registro. O JSON do asset também tem formato diferente do registro C# legado.

### Hash e informações do catálogo

O modelo `StationCatalogEntry` e o carregador `StationLibrary` permanecem
idênticos à base e não publicam `contentSha256` na resposta do catálogo.
Acrescentar esse campo apenas ao JSON privado do índice não o fará chegar ao app.
A integração precisa incluir leitura, validação, serialização assinada e cache
do campo, com SHA do conteúdo realmente aberto no emulador; hash do container
ZIP não substitui esse vínculo.

A entrega pesquisou 2.467 IDs do export anterior: 2.212 visíveis; 1.454
correspondências descritivas candidatas, 734 sem correspondência e 279 divergentes.
Não é catálogo factual completo nem nova conferência autenticada da produção.
O asset factual continua com zero registros, e o contador mostra `—` até receber
evidência vinculada a ID, revisão e conteúdo exatos. O piloto Super Bomberman 2
continua proposto, sem aprovação física dos quatro controles.

## Ordem para uma futura ativação

1. Conciliar a candidata com a fonte ativa, compilar em ambiente isolado e
   repetir os gates necessários de autenticação, v2/v3, canais e recuperação.
2. Conferir o conteúdo real e preparar perfis exatos do piloto e dos jogos v2
   preservados; validar controles e aprovação por modo com os aparelhos.
3. Integrar o campo factual do catálogo e definir o fluxo de convites/códigos v3
   antes de substituir a experiência atualmente usada.
4. Preparar pacote, backup, rollback e prova em instância isolada; coordenar
   uma ativação sem salas retidas, preservando os demais produtos e dados.
5. Confirmar capabilities/perfis em resposta autenticada fresca e realizar
   os ensaios físicos separados de duas, três e quatro pessoas.

Esta revisão não comprova correção dos engasgos R76, latência de Internet,
gameplay R77, três/quatro celulares ou qualificação da DLL `6f27`.
Nenhum serviço, banco, licença, engine efetivo, rota ou configuração foi alterado.
