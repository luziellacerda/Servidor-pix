# SERVIDOR → APP — R78: catálogo atual e sinopses — 08/10/2026

Entrega recebida no servidor: `525f03735cbec194e942c91417d38a4d799c8910`.
Fonte executável do app: `ba145fff4bcf2c905ac6eb62916d7e854ec31b09`.
Documentação de geração: `37a7e813b707fed2a28c8e11dbe7fb992cf62368`.
**Adendo de instalação recebido durante esta revisão:**
`768f7ca42fe62d67e93f4c8fcf762bf9e7eb4fce`.

Esta rodada lê, compara e publica o retorno. Nenhuma proposta foi aplicada ao
índice, nenhuma DLL foi implantada e nenhum serviço foi reiniciado.

## 1. Catálogo efetivo: revisão 18

A leitura autorizada, somente de metadados, resolveu `Station__LibraryIndexFile`
do processo Station em execução e leu o arquivo configurado. Não foi usada uma
cópia escolhida por nome. Coleta: **2026-10-08T14:53:30.950696Z**, PID **1278094**.
SHA256 do arquivo: `c237c97658c8e8bb9b14fb9954d4c10885717eecd2685a3ad066addc14e4879f`.

O índice está na **revisão 18: 3.734 IDs, 3.479 visíveis e 255 de compatibilidade**.
A base da R78 era a revisão 14: 2.467 IDs e 2.212 visíveis. Foram adicionados
**1.267 IDs**; nenhum dos IDs anteriores foi removido. Nos 2.467 IDs comuns,
coincidem os campos comparados: nome, plataforma, visibilidade, revisão do item,
SHA/nome/membro do artefato e descrição publicada. Isso não compara todos os
campos de metadados nem comprova os bytes dos jogos.

| Plataforma | IDs atuais | Visíveis | Descrição vazia no servidor | Novos desde R14 |
|---|---:|---:|---:|---:|
| Mega Drive | 1.133 | 887 | 7 | 0 |
| Mega Drive BR | 94 | 94 | 1 | 0 |
| SNES | 653 | 644 | 10 | 0 |
| SNES BR | 191 | 191 | 0 | 0 |
| Nintendo 64 | 157 | 157 | 4 | 0 |
| Neo Geo | 189 | 189 | 27 | 0 |
| Neo Geo CD | 50 | 50 | 0 | 0 |
| Dreamcast | 243 | 243 | 243 | 243 |
| CPS1 | 36 | 36 | 7 | 36 |
| CPS2 | 64 | 64 | 5 | 64 |
| CPS3 | 11 | 11 | 3 | 11 |
| FBNeo | 913 | 913 | 28 | 913 |
| **Total** | **3.734** | **3.479** | **335** | **1.267** |

Há **3.399 descrições presentes e 335 vazias**, sendo 329 vazias visíveis e seis
de compatibilidade. Não foram encontradas descrições compostas apenas de
espaços, placeholders da lista exata R78, títulos isolados ou textos acima de
2.000 unidades UTF-16. É uma verificação estrutural, não revisão factual integral.

Das 335 vazias, **49 pertencem aos IDs antigos e têm fallback exato na R78**.
As outras **286 são IDs novos sem descrição e sem fallback R78**: Dreamcast 243,
CPS1 7, CPS2 5, CPS3 3 e FBNeo 28. Portanto, a cobertura de 2.467 IDs não cobre
todo o catálogo atual. Não escrever sinopses genéricas para preencher esses casos.

Arquivos entregues em `recovery-r78-20261008/`:

- `COMPARACAO-CATALOGO-REV18.json`: contagens, campos comparados e limites.
- `CATALOGO-NOVOS-IDS-REV18.json`: todos os 1.267 IDs novos com nome, plataforma,
  revisão, `coverId`, descritor existente e metadados, inclusive as 981 descrições
  já presentes. Sem caminhos privados, URLs de download ou credenciais.
- `SINOPSES-AUSENTES-REV18.json`: os 335 IDs, com classificação, presença de
  fallback e proposta; filtrar `addedSinceRevision14:true` para os 286 pendentes.
- `comparar-sinopses-r78.py`: comparação reproduzível sobre export privado de
  metadados. Não acessa produção, configuração, rede ou ROMs.

Esta leitura comprova o **arquivo efetivo configurado**. Não é uma resposta
autenticada fresca do catálogo nem leitura dos caches dos telefones; não declarar
que todos os aparelhos já receberam a revisão 18.

## 2. As 64 propostas coincidem; nenhuma aplicada nesta leitura

Os 47 arquivos DELIVERY tiveram tamanho/SHA conferidos; o manifesto é o 48º
arquivo recebido. Os 45 arquivos SOURCE foram conferidos contra o commit
executável `ba145fff`, não contra o recibo documental posterior.

As **64 propostas** satisfazem literalmente todas as precondições na coleta R18:
ID, plataforma, revisão do item, SHA do artefato, descrição anterior e SHA UTF-8
dessa descrição. Textos propostos têm hashes válidos e cabem no limite de 2.000
UTF-16. Fontes declaradas: seis aliases por descritor, 17 editoriais R37,
27 XMLs exatos R37 e 14 editoriais R78.

| Estado desta revisão | Quantidade |
|---|---:|
| Precondições coincidentes | 64 |
| Precondições divergentes | 0 |
| Propostas aplicadas | 0 |
| Propostas rejeitadas por operação de escrita | 0 — não houve escrita |
| Descrições vazias que essas propostas preencheriam | 49 |
| Textos longos excluídos do campo servidor | 17 |

`CAS-PROPOSTAS-R78-REV18.json` contém os IDs, revisão e hashes anteriores/propostos.
A coincidência observada não reserva o índice: uma incorporação deve repetir o
CAS no momento da escrita, preservar todos os outros campos/IDs/artefatos/capas,
preparar backup e avançar a revisão pelo fluxo vigente. O monitor existente exige
revisão crescente e mantém o último snapshot válido se recusar uma recarga.
Conferir também o importador automático para que não sobrescreva a descrição.
Não executar uma cópia do catálogo R14 sobre o índice R18.

Os **17 textos longos continuam apenas no fallback nativo**. Suas identidades e
hashes anteriores também coincidem nesta coleta, mas o Java/servidor rejeita
descrições acima de 2.000 UTF-16. Preservar o contrato e o texto completo nativo.

## 3. Como o app deve obter e mostrar a descrição

1. Usar a sessão vigente e solicitar
   **`GET https://app.lzgames.com.br/v1/station/catalog?metadata=1`** pelo cliente
   Station existente. Sem `metadata=1`, a resposta deliberadamente omite o objeto
   `metadata`. A fonte Java herdada pela R78 **já solicita esse parâmetro**.
2. Validar o envelope assinado/domínio e vínculo da sessão pelo fluxo existente.
   Ler revisão do catálogo, ID, plataforma, revisão do item, `coverId` e
   `metadata.description`; conservar o limite de 2.000 UTF-16.
3. Vincular capa por **`coverId` do mesmo item**, usando a rota autenticada
   `/v1/station/covers/{coverId}`. Não deduzir capa, ID ou plataforma pelo nome.
   Os JSONs deste retorno são metadados de revisão, não substitutos do catálogo
   assinado nem novos endpoints de download.
4. Para sinopse, prosa válida nova do servidor prevalece. Fallback somente por
   ID/plataforma exatos; override editorial somente para o texto anterior exato,
   bruto ou paginado. Nome semelhante ou outro `spy.zip` não autoriza herança.
5. Atualizar o catálogo/cache pelo mecanismo normal do app. Para fechar o
   diagnóstico de apresentação, registrar a revisão autenticada e o ID observado
   em cada aparelho e conferir a ficha. Esta revisão não limpou caches nem dados.

A presença de texto é independente de contagem factual de jogadores, suporte
do core, perfil online aprovado e `contentSha256` do conteúdo emulado.

## 4. S.P.Y.: classificação separada

O índice atual contém dois itens chamados S.P.Y. World N:

- Neo Geo: `station_f9b5624a8652bfaccf2e3c338510d29b`, revisão 9,
  artefato SHA `51a2c1a3f8d1102f13cf5e3777712b7ada1ec22a6e254b5626686508bda38b14`.
- FBNeo: `station_9b092804dfa0e60e1fe866d9472459b0`, revisão 18,
  artefato SHA `5230360749d6f9d677007f86516cbda4d69d0d44b880edac843e5d27cc2c2a8f`.

O driver oficial [FBNeo](https://github.com/finalburnneo/FBNeo/blob/master/src/burn/drv/konami/d_spy.cpp)
identifica `spy` como Konami/GX857; o [MAME](https://github.com/mamedev/mame/blob/master/src/mame/konami/spy.cpp)
também o registra como Konami. Isso sustenta revisar a classificação de hardware
da entrada Neo Geo. Não comprova o conteúdo dos dois pacotes locais, que têm
hashes e tamanhos diferentes. Conferir sua identidade e o despacho do core antes
de qualquer migração. **Nenhum ID, plataforma ou autorização foi alterado.**

## 5. Adendo recebido: R78 instalada nos dois

Commit `768f7ca` registra instalação solicitada pelo mantenedor, posterior ao
congelamento da entrega. Motorola Edge 30: **14:41:44 UTC**; Samsung A56:
**14:44:57 UTC**. Ambos receberam o APK
`e6c6609df32bf419954083297eadbafa75f13328a1516594cc010044b2eb62a5`.
Os recibos PCAPK confirmam SHA integral, UID, data original e diretório de dados
preservados, sem limpeza/desinstalação. O Linux conferiu os recibos recebidos,
não leu novamente o APK dos aparelhos.

Os `installed:false` históricos de geração foram superados pelos recibos de
instalação. Abertura autenticada, aparência e gameplay **não foram confirmados**
por esses recibos. Versão comprovada atual dos dois: **R78**, não R76.
`INSTALACAO-R78-RECIBOS-PCAPK.json` conserva os campos relevantes e hashes dos
três recibos, sem identidades privadas ou comandos USB.

## 6. Produção e online

Station permanece ativo, PID **1278094**, NRestarts **0**; prontidão consultada
durante a revisão mostrou zero salas/conexões/pendências e protocolo
**station-stream.v2**. DLL `ab192bf`/`815fc8bc` e os dez engines R74/R76 permanecem
identificados pelo recibo anterior; não houve nova rehash privilegiada da DLL.

R78 altera somente a apresentação nativa de sinopses sobre R77. Herda o novo
runtime/core e as dependências v3 da R77; a instalação nos telefones não ativa
esse protocolo no servidor. O retorno
`RETORNO-LEITURA-HANDOFF-R77-20261008.md` continua válido para integração:
perfis exatos aprovados, inclusive admissão legado v2, campo factual de conteúdo,
convites/códigos e qualificação. Não inserir engines v3 no registro legado nem
ativar os flags com o piloto `approved:false`.

Não houve nova compilação ou execução dos testes PCAPK. A candidata `6f27`
continua sem implantação; esta revisão editorial não resolve sua divergência
TLS nem os engasgos R76. Bancos, licenças, rede, serviços e outros produtos
permanecem preservados.
