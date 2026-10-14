# R149 no A56 — Wind Waker abriu no Dolphin e falhou ao ler a imagem

Registro de 14/10/2026. Este diagnóstico é **offline** e se refere somente ao
item `station_12b835c9539b4800a6bb29ca00a25f94`. A R149 assinada está
instalada no Samsung A56. Nesta tentativa, o lançamento ultrapassou o antigo
`LinkageError` de `onPause()` e entrou no Dolphin. Isso confirma a passagem
pelo ponto que antes bloqueava a preparação; pausa, retomada e uma partida
completa ainda não foram validadas.

## Evidência física e correspondência do catálogo

O Dolphin exibiu `The disc could not be read (at 0x55e3c040 - 0x55e3c060).`
A leitura começa no byte **1.440.989.248** e termina antes do byte
**1.440.989.280**. A imagem `.iso` instalada para este item mede somente
**546.776.387 bytes**. O início da leitura está **894.212.861 bytes além do
fim do arquivo**. O SHA-256 local é
`3106a01516f59854a55fd6343da8b0f489ca8cfc523f300bb1913d7f1c602a2f`,
igual ao descritor publicado. O cabeçalho é `GZLE01`, embora o nome do item
indique Europa. Portanto, a transferência reproduziu o artefato cadastrado;
o hash coincidente comprova identidade de bytes, não integridade de um disco
jogável nem a região anunciada.

A captura da mensagem e os registros da tentativa foram preservados no backup
privado `F:\TurboStations-Backup-Completo-20261011\desenvolvimento\R149-dolphin-onpause-20261014-provas\teste-fisico-a56`.
SHA-256 da captura: `d40bd98a4978a24109dd6bf3a7f35a3ddf29bc2fbd2aa2e4fcb285ce4183b05a`;
SHA-256 do registro bruto: `ae1487e0a559c19d98f406129a458354f6f525fa8569b49eb5a343698bc19b67`.
O registro bruto não é publicado no GitHub.

O código do próprio Dolphin mostra que a mensagem é emitida quando a leitura
do disco não devolve o comprimento solicitado: [DVDThread.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/HW/DVD/DVDThread.cpp#L240-L279).
No GameCube, `VolumeGC::Read` encaminha a leitura ao leitor da imagem:
[VolumeGC.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DiscIO/VolumeGC.cpp#L54-L60).
O tamanho padrão de disco GameCube consta em
[DiscUtils.h](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DiscIO/DiscUtils.h#L17),
e o verificador do Dolphin identifica imagens incompletas em
[VolumeVerifier.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DiscIO/VolumeVerifier.cpp#L703-L742).
O erro concreto aqui também é demonstrado diretamente pelo intervalo fora
do arquivo, sem depender de presumir que toda imagem menor seja defeituosa.

A entrada original está em
`docs/station-android/novos-sistemas-20261008/novos-jogos-capas-artefatos.json`
(linhas 5–29): `catalogVisible=true`, `format=raw`, `fileName`/`launchPath`
terminados em `.iso`, tamanho e SHA acima. O snapshot cruzado, revisão 44,
em `docs/station-android/online-plataformas-20261009/catalogo-cruzado-completo.json`
(linhas 310872–310896) conserva esses valores. O mesmo snapshot tem outro
item visível, `station_d0a6aa65e5900ba9167a200f8dccfae5`, chamado
`The Legend of Zelda The Wind Waker`, com ISO RAW de **1.459.978.240 bytes**
e SHA-256 `3ea3cc8f62acfaafe2d2adb96090bf87cfabd01f89e0631d571517faf670d5f1`
(linhas 312024–312039). É uma **candidata a verificar**, não substituição
automaticamente aprovada: ainda faltam conferir o arquivo, ID de região,
verificação do Dolphin e gameplay físico dessa edição.

A R149 preservou integralmente a lógica de download/instalação da R148.
`StationInstaller.java` confere tamanho, formato, caminhos e conclusão
transacional, depois copia o RAW; por política anterior, não avalia se o
conteúdo é um disco GameCube funcional. O SHA foi conferido nesta investigação
independentemente. Não há evidência para recompilar o APK por esta falha.

## Quarentena cirúrgica no servidor

1. No índice produtivo **efetivamente configurado** no Linux, obter baseline
   fresco, backup e recibo sob o bloqueio de varredura vigente. O caminho
   histórico da biblioteca automática é
   `/mnt/DADOS/turbostation-library-auto-20261004/index.json`; confirmar no
   serviço/configuração atual antes de operar. Preservar a imagem original e
   seus hashes para auditoria, sem sobrescrever outro artefato.
2. Suspender **somente** `station_12b835c9539b4800a6bb29ca00a25f94`:
   removê-lo da apresentação (`catalogVisible=false`) **e** retirar seu
   descritor `artifact` do índice vigente, com revisão de catálogo avançada
   atomicamente. `catalogVisible=false` sozinho é insuficiente:
   `StationLibrary.Catalog`/`FindCatalogItem` filtram visibilidade, mas
   `StationLibrary.TryResolveArtifact` e `StationService.AuthorizeDownloadAsync`
   continuam aceitando o ID conhecido quando há `artifact`. Conferir por
   API que o item não aparece e que nova autorização desse ID é recusada,
   preservando a autorização de um item de controle.
3. A varredura horária conserva o registro quando a impressão da fonte não
   muda, mas pode reconstruir o descritor se a fonte mudar. Manter o arquivo
   de origem em quarentena controlada fora do escopo de republicação, ou
   registrar a exclusão de modo persistente na autoridade do scanner, sem
   alterar outras edições. O monitor mantém snapshots antigos por até
   **120 segundos** para grants já emitidos; aguardar expiração dos grants
   antes de declarar o bloqueio efetivo. Não reiniciar a API por esta ação.
4. Verificar a imagem original com `dolphin-tool verify -i` e ler seu cabeçalho,
   tamanho, estrutura e região. Se houver uma imagem válida, publicar novo
   artefato imutável com SHA/tamanho/ID/região/nome correspondentes, nova
   revisão e prova de leitura; não renomear a imagem atual como se fosse
   íntegra nem trocar bytes sob o SHA antigo. Conferir também a candidata de
   1.459.978.240 bytes antes de oferecê-la como alternativa.
5. No A56, a instalação antiga desse ID não se substitui só por revisão nova:
   `StationInstaller.find(item)` devolve o caminho da instalação gravada e
   não exige igualdade com `item.revision`. Após a publicação válida, usar a
   remoção **deste jogo** pelo aplicativo, preservando saves e dados gerais,
   baixar a edição correta e repetir abertura, leitura, pausa, retorno e saída.
   Uma nova identidade de item também evita associação acidental ao ISO velho.

O snapshot documental revisão 44 contém 21 itens GameCube RAW; **oito** têm
tamanho abaixo do offset final desta falha e **cinco** não são múltiplos de
2.048 bytes. Esses são candidatos para uma auditoria de mídia, não provas de
defeito ou autorização para ocultá-los em lote. Os oito incluem esta edição
de Wind Waker, Pikmin 2, Prince of Persia: Two Thrones, os dois discos de
Resident Evil Code Veronica X PT-BR, Sonic Heroes DUB, Sonic Adventure 2 e
Terminator 3. Inspecionar cada fonte e cada formato individualmente.

## Limites e separação do kit online

Este erro de leitura acontece depois do boot offline do Dolphin e **não é
causado pelo cadastro do kit de motores independentes**. A R149 continua
candidata; o conserto do `LinkageError` avançou até o Dolphin, mas Wind Waker
não passou do teste de mídia. O kit privado R149 segue sem recibo de aplicação
no servidor; N64/GameCube/Wii/PSX online exigem seu cadastro e testes próprios.
Não reaplicar R141, mexer em banda, outros jogos, motores, controles ou lógica
de download como consequência deste incidente.
