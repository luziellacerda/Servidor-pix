## Samsung A56 também atualizado para R141 — 13/10/2026

Samsung SM-A566E/Android16 atualizado da R138 realmente encontrada para o mesmo APK R141 `b5a76023…`, fonte executável `240b7c05…`. SHA integral instalado, assinatura original, UID, dados e primeira instalação preservados. Nenhuma recompilação, alteração de código/configuração ou limpeza de dados. Moto g52 e A56 agora compartilham a R141; Edge30 permanece na última versão temporária de diagnóstico conhecida. [Recibo A56](evidence/installation-samsung-a56.json). A primeira tentativa recusou o baseline histórico R135 sem instalar; a segunda confirmou a R138 atual e atualizou com as mesmas proteções. Instalação não comprova gameplay ou cadastro dos quatro novos IDs.

## Resultado atual: R141 assinada, instalada e download Android concluído

Reconstrução salva e conferida no backup F existente: **403 arquivos**, bundles incrementais dos dois Gits e captura Android privada; versões anteriores e ponteiros de recuperação preservados. [Recibo do backup](evidence/backup-verified.json).

Fonte executável congelada `240b7c0504252e542975089f585bd67d5264f074`. APK SHA256 `b5a76023860bceb2aa11c946c8003fa2d92951e700dbde3a498f8316857670d4`, 2.715.008.015 bytes, certificado original v2/v3 e 16 KiB. Todas as 13.860 entradas finais foram conferidas. Cinco substituições/13.855 entradas e 39 DEX preservadas; DEX35 R140, runtime ae72, nove descritores RetroArch, cores/opções/HLE/READY/cache/FPS/controles exatos. A revisão independente das provas e sete entradas finais passou. A falha da primeira tentativa foi só na API do helper; a correção e a nova compilação estão registradas, sem alterar as duas fontes HTTP recebidas.

Moto g52 instalado por atualização com SHA integral/UID/dados/primeira instalação preservados. O frontend automático não foi observado pelo instalador; o operador depois abriu o app e realizou um download. Autorização200 e artefato200; **546.776.387 bytes em 75.288 ms**, média de **7,26 MB/s decimal**. Preparação do jogo: 5.862 ms; `INSTALL_FINISHED` registrado. Seu contador soma arquivo+expansão, portanto 1.093.552.774 não representa bytes adicionais de rede. Nenhum evento de falha do download dessa tentativa; o serviço de download não consta na leitura posterior. O título não foi informado. O operador relatou “mais que dobro velocidade”; essa percepção é preservada, sem apresentar uma razão medida, pois não houve comparação pareada com a origem antiga. Destino direto decorre do código instalado; Host/socket não foi observado independentemente. Cancelamento/regrant Android e gameplay não foram testados nesta tentativa.

BAT/PowerShell preservados, manifesto USB atualizado para R141. Edge30 última R139-DIAG conhecida, A56 R135. Ponteiros de lançamento permanente conservam a R139 de recuperação; esta é a compilação de teste atual.

**Kit privado entregue e SHA relido em I:/SERVIDOR LER:** 12.842.618 bytes, SHA `d8d9746d492866d5f279f3fbb1f5c0ab64c99db3f81a26e654c67baef4afc1b9`, 38 arquivos, seis bibliotecas/vinte dependências, quatro IDs renovados, incoming v3 e referências exatas de 78 modos. Cadastro da combinação R141 ainda pendente de recibo real; não confundir com e696 já cadastrado. Os oito IDs RetroArch permanecem iguais. Nenhuma implantação/reinício/aplicação administrativa foi realizada por esta entrega.

Evidências: [download Android](evidence/android-download-moto-g52.json), [instalação](evidence/installation-moto-g52.json), [prova final](evidence/signed-apk-final-proof.json), [preservação independente](evidence/compiled-preservation-review.json), [kit entregue](evidence/private-independent-kit-delivered.json).

## Preparação e contratos preservados abaixo

# R141 — downloads diretos sobre a R140 preservada

## Estado na preparação

O novo setor do servidor foi lido no Git e no pacote USB indicado pelo mantenedor. O APK recebido `e69616ee…` parte da R139. Todas as 13.860 entradas foram conferidas: comparado à R140, também substitui `classes35.dex` pela versão antiga e retiraria as duas flags que fixam as opções online. Esse APK não foi instalado.

A composição R141 parte do APK R140 `61e873e7…`, com DEX35 `49f381dc…` exato. A R140 já está instalada no moto g52: SHA integral, assinatura, UID, dados e primeira instalação preservados. O logger temporário foi retirado. O recibo de instalação não observou o frontend; uma leitura posterior encontrou a LoginActivity visível. Partida não foi homologada. Edge 30 permanece na última versão temporária de diagnóstico conhecida e A56 na R135. A R140 e sua reconstrução foram salvas no backup F existente; os ponteiros de lançamento permanente conservam a R139.

## Contrato de downloads

Somente GET canônico `/v1/station/artifacts/{grantId}` usa `https://downloads-station.lzgames.com.br`. Autorizar continua em `POST /v1/station/downloads/authorize` na origem original. Login, sessões, catálogo, capas, mídia e comandos permanecem na origem original. CA pública, hostname, pin próprio, Bearer, prova por pedido e correlação são preservados. Redirects continuam recusados. Não há fallback com o mesmo grant.

Conservam-se um trabalhador, fila 32, transferência sequencial, buffers, limites de progresso/notificação, timeouts 10/30 segundos e cancelamento. Interrupção descarta o parcial; nova tentativa pede nova autorização. O grant permanece de uso único com prazo de 60 segundos e vínculos de licença/aparelho/sessão. O descritor assinado, tamanho/formato/caminhos e publicação atômica continuam validados. Hashing de conteúdo no Android permanece desativado conforme a política já existente; não acrescentar hashing como tuning.

## Preservação e cadastro

A fonte Java revisada altera somente a família StationHttp e acrescenta StationDownloadOrigin. O overlay deve preservar 266 tipos da partição 28, inclusive ResponseBody. A montagem exige cinco substituições: DEX28, engines.json e três wrappers vinculados às 345 cargas do APK. São 13.855 entradas e 39 partições DEX preservadas, zero adições/remoções; DEX35 e as duas flags R140 permanecem exatos. Nenhum core de emulador ou runtime RetroArch é recompilado.

Os quatro IDs cadastrados pelo servidor pertencem ao APK recebido `e69616ee…`. A combinação R141 terá quatro IDs próprios e exige cadastro dos bytes reais correspondentes; o cadastro recebido não prova o cadastro da combinação. Os oito IDs RetroArch ativos e nove descritores são preservados. O servidor declara 35 motores/11.526 perfis/6.665 modos após acrescentar 78 vínculos ao pacote recebido, preservando os anteriores e sem reiniciar a API.

## Provas e limites

O retorno registra 43 checks de contrato, três cenários TLS e download autorizado de 4 MiB no vhost local. Reuso, sessão estrangeira, expiração e fluxo abandonado foram recusados; nova autorização funcionou. Esses recibos são do produtor anterior e do servidor, não execução Android nem medição da Internet do consumidor. Os mesmos testes não foram repetidos nesta fase de preparação.

A versão combinada ainda precisa dos seus próprios recibos de compilação, assinatura, entrada por entrada e instalação. Depois: download pequeno autorizado pelo app, cancelamento/nova autorização, conclusão/publicação normal e liberação do serviço, mantendo a tela responsiva. Comparação de velocidade requer redes/artefatos equivalentes e grants diferentes. Não alegar ganho sem medida.

O retorno online reconhece quatro capturas independentes de Top Gear com falha DSP4 e o gate de vinte cenários do resolvedor R140. Arquivo legado efetivo e gameplay Android ainda não foram comprovados. Duas recusas 409 do relay têm motivo desconhecido; capturar código seguro no próximo teste sem relaxar proteções. Não incorporar logging extra, mudanças de READY ou novos retries nesta composição de downloads.

Fonte da composição e receita: `integration/direct-downloads-preserved-r141-20261013` no repositório do aplicativo. Evidências sanitizadas estão em `evidence`; binários, assinador privado, chaves, logs brutos, ROMs/BIOS e identificadores de telefone permanecem fora do Git. Este documento amplia o mesmo handoff vigente, sem substituir o histórico.
