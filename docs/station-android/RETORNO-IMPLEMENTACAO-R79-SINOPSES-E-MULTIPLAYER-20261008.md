# Retorno R79 — sinopses aplicadas e candidata online conciliada

Recebida a entrega servidor `706f47ff06659feb777d3c4c999b645cb5c3a1ff`, fonte executável Android `b377db3981c96374fbe5591700db91dfbe297994`. Os 31 registros DELIVERY e 29 registros SOURCE foram conferidos. A organização posterior do app está em `c73e087b28a59f05bf70029dd8936658a28610dd`; ela mantém os canais R76 e R79 e supera os caminhos históricos de APK em E:.

## Produção: 206 sinopses realmente aplicadas

Aplicação em **08/10/2026 às 18:04:18 UTC**, mediante autenticação nativa Linux. Revisão **18 → 19**, mantendo **3.734 IDs, 3.479 visíveis e 255 de compatibilidade**. Foram incorporadas 64 propostas R78 e 142 de Dreamcast da R79: 191 descrições vazias preenchidas e 15 descrições existentes atualizadas. Agora há **3.590 descrições presentes e 144 vazias**.

O operador conferiu literalmente ID, plataforma, revisão do item, SHA do artefato, descrição anterior e seu SHA antes da escrita. Preservou nomes, IDs, revisões dos itens, coverId, caminhos, artefatos e demais metadados. Atualizou também os overrides do importador sob o mesmo `scan.lock`: somente escrever o índice perderia as alterações na próxima varredura. Backup privado criado; rollback guardado pelos hashes posteriores e com avanço de revisão, sem regressão do monitor.

Nova leitura às **18:14:45 UTC**, após varreduras automáticas, confirmou os mesmos 206 textos e SHA do índice `c673e80c4200a3b37ad07c586894fda4370e08702e079a4775cecb6b9aef18ed`. Às **18:21:17 UTC**, uma licença sintética descartável consultou **https://app.lzgames.com.br/v1/station/catalog?metadata=1**, com prova RSA e verificação da assinatura vigente: revisão 19 e todos os metadados dos 3.479 itens visíveis coincidem. Dessas alterações, 200 estão na resposta visível e seis são IDs de compatibilidade. A fixture foi removida; nenhuma sala criada nem jogo baixado nesse teste.

Sem reinício do Station: **PID 1278094 / NRestarts 0**, DLL ativa continua `ab192bf` / `815fc8bc…` conforme recibo anterior, sem nova rehash privilegiada nesta rodada. V2 e dez engines R74/R76 preservados. Capas, bytes dos jogos, downloads, licenças reais, chaves e configuração de rede não foram alterados.

Dados completos em [recovery-r79-20261008](recovery-r79-20261008/): catálogo dos 3.734 IDs com nomes/plataformas/coverId/metadados/descritores, 206 alterações e 144 ausentes. Os 255 itens de compatibilidade vêm do índice; não foram apresentados como resposta do catálogo visível. Caminhos absolutos privados, credenciais e envelopes de sessão não foram publicados.

## Como o app deve consumir a revisão 19

1. Usar a API `app.lzgames.com.br`, sessão do aparelho, prova por pedido e verificação do envelope conforme o contrato já implementado. Solicitar **`GET /v1/station/catalog?metadata=1`**. Sem `metadata=1`, a descrição continua omitida intencionalmente; ambos os casos foram conferidos no domínio público.
2. Reconstruir o catálogo recebido e persistir `metadata.description` e os outros cinco campos. A revisão global mudou para 19; a revisão de cada jogo permaneceu. A atualização de ficha não deve exigir baixar o jogo novamente. `StationCatalog.fromVerifiedPayload` da fonte R77 herdada pela R79 relê os metadados e `localPayload` os preserva.
3. Vincular a capa pelo **coverId do próprio item**, usando a rota autenticada de capas já contratada. Vincular autorização de download pelo itemId e usar o descritor retornado pelo fluxo vigente. Não correlacionar capa/jogo por posição da lista ou título aproximado.
4. Texto novo válido do servidor prevalece sobre o fallback nativo. Os 17 textos R78 longos continuam somente no nativo; o limite de 2.000 unidades UTF-16 do servidor foi preservado.

Dreamcast recebeu 141 textos do XML por plataforma/título completo com normalização limitada de caixa/espaço. Para `102 Dalmatians - Puppies to the Rescue`, o texto de origem excedia o limite: foi publicada uma síntese original em português, baseada naquela descrição. Os cinco títulos conflitantes do XML continuam excluídos. Não houve tradução integral nem homologação factual de todas as descrições; esse vínculo editorial não aprova jogadores ou motores.

**144 lacunas restantes:** Dreamcast 101, CPS1 sete, CPS2 cinco, CPS3 três e FBNeo 28. A lista contém IDs e capas exatos para a próxima revisão editorial; não preencher por semelhança. O cache e a aparência da revisão 19 nos telefones ainda não foram observados por este servidor.

## Candidata v3: implementada, compilada e testada; ainda sem implantação

Fonte funcional **`b472d8a653e065cccb00dfb15dbea3d56c03db98`**, DLL candidata **`9878ae9caea55bb6834745caa3a60140616d3e0a5f2ea71055fe9e9df813fe51`**, SDK 8.0.131 / runtime 8.0.31. Release privada independente; o executável ativo não foi substituído.

O código R77 foi integrado em `src`, conciliando a candidata de observabilidade `6f27c6c`. Permanecem a captura da primeira causa antes de Detach, Close pelo escritor único, métricas limitadas, rota local protegida de diagnósticos e seus testes. A reserva compartilhada v2/v3 só é liberada após o último anexo sair; os limites do protocolo antigo permanecem.

- Novos endpoints candidatos: `POST /v1/station/online/multiplayer/command` e WSS `/v1/station/online/multiplayer/relay`; protocolo `station-stream.v3`. Corpo limitado a 8.192 bytes, sessões/provas existentes, tickets de uso único, vínculos de sala/link/slot/geração, pausa global e replay limitado.
- O catálogo candidato carrega, valida e assina **`contentSha256` opcional**, inclusive sem `metadata=1`. Valor ausente permanece ausente; SHA do ZIP não é convertido em SHA do conteúdo. Nenhuma leitura de ROM foi acrescentada à consulta, ingresso ou download.
- Perfis são confrontados com plataforma **e hash do catálogo atual**. Hash desconhecido ou substituído oculta o perfil nas capabilities e recusa sua autorização. A aprovação antiga não continua válida após troca de conteúdo.
- O importador candidato aceita `contentIdentityRegistry` privado com identidades qualificadas offline, vinculadas a itemId, plataforma, SHA do artefato, launchPath, tamanho expandido e quantidade de arquivos. Preserva o campo em varreduras estáveis e o retira quando esse vínculo deixa de coincidir. O exemplo publicado é sintético. **Esse importador e essa configuração não foram instalados em produção.**

Testes executados no Linux: **288** checks de hub/stream/catálogo/vínculos; **152** de HTTP/TLS/WSS com quatro usuários e seis links sintéticos; **91 + 589** de estado v2 e observabilidade preservada; **41 + 53** sociais/HTTP legados; nove testes do operador de sinopses, quatro de identidade e cinco de descoberta. Regressões gerais de assinatura/provas, isolamento de produto, extração e download/NAT passaram. Integração PostgreSQL isolada não foi executada nessa suíte. Recibo e hashes dos 19 arquivos funcionais em `QUALIFICACAO-CANDIDATA.json`.

Esses testes não demonstram gameplay Android ou estabilidade WAN. A divergência anterior do ensaio TLS v2 sem logging da candidata 6f27 continua sem conclusão; o novo teste TLS v3 não a resolve por equivalência.

## O que continua impedindo ativar a R79 online

**Zero perfis reais aprovados.** O piloto recebido de Super Bomberman 2 continua `approved:false`. Não foi publicada uma identidade factual de conteúdo na revisão 19, e a DLL ativa ainda não possui o novo serializador. Battletoads e o piloto não têm capabilities v3 efetivas aprovadas em produção; os exemplos e testes sintéticos não foram apresentados como respostas desses jogos.

O contrato recebido exige `MultiplayerLegacyCapacityGate` ao ativar v3. Esse gate também recusa **novas salas v2**, inclusive de duas pessoas, sem perfil exato `standard-2p-v1` aprovado para item/conteúdo/engine/core/runtime. Ativar o arquivo piloto atual impediria partidas que a R76 ainda consegue criar. As flags permaneceram desativadas; os motores v3 não foram misturados no registro legado, que aceita somente v2.

A próxima publicação precisa qualificar os conteúdos reais, os perfis e modos, preparar a continuidade dos títulos legados e coordenar o teste do canal experimental. Convites/códigos v3 também continuam ausentes da candidata recebida; não declarar a experiência antiga migrada. Depois disso, ensaiar backup/retorno da release, qualificação v2 pendente, sombra e domínio público, e trocar somente Station quando não houver sessões retidas. O processo continua sem recuperação de RAM após reinício.

## Canais e aparelhos do PCAPK

Preservado o backup definitivo `G:\BAKUP SISTEMA APP 03-10-2026\ATUAL-2P-E-TESTE-4P-20261008`: **stable-2p R76** e **test-4p R79**. Ler `release-channels/ACTIVE.json` e usar as fontes/receitas completas desses canais. Caminhos antigos de instaladores foram aposentados por decisão do mantenedor; não restaurá-los por recibos históricos.

Última instalação comprovada: **Motorola R79 / Samsung R78**. A organização dos backups não alterou os aparelhos. A referência R76 foi escolhida pelo mantenedor e mantém o relato anterior de engasgos; instalação R79 não significa ativação v3. Nenhum telefone foi compilado ou instalado por esta rodada Linux.
