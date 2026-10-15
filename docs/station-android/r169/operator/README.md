# Preparador R169 sobre a R168 aplicada

A autoridade R168 foi aplicada no servidor segundo
[`server-return.json`](../../r168/server-return.json): 91 motores, 16.896 perfis,
11.981 modos, catálogo revisão 100 com 4.744 itens. O limite WinBack por conteúdo
foi corrigido para 256 KiB e validado nesse ciclo. O Android R169 instalado em
dois telefones ainda recebe, para *Capcom vs. SNK — Millennium Fight 2000 Pro*,
“não há modo aprovado para esta edição e o motor instalado”. A R169 **não foi
cadastrada** no último estado remoto confirmado.

`prepare_all.py` é apenas um preparador local, sem rede ou implantação. Recebe
o ZIP privado R169 de hash SHA-256
`859b33b156698abecadadf035f8117a4b2b680439c4fed32eb2001347dc6c832`
e os quatro registros **atuais** exportados da autoridade R168: catálogo,
perfis, modos e manifesto de motores. Confere cada arquivo do kit, prova do APK
assinado, os quatro novos `engineId`, política nativa, perfil de controles e
vínculos existentes. Recusa qualquer alteração na edição, conteúdo, core,
opções, aprovações e baseline. Prepara 492 modos/perfis aditivos: 93 PSX
(23 VS/2P aprovados, 17 pendentes, 53 classificações locais), 387 N64,
9 GameCube e 3 Wii. Nenhum perfil ou modo anterior é removido ou modificado.
O N64 preserva a lógica existente; somente a nova identidade vinculada ao APK
ganha registro próprio.

## Execução no servidor

Usar os arquivos reais da autoridade aplicada, identificados pelo operador a
partir de `/mnt/DADOS/station-r168-capacity-20261015/completed.json` e da
configuração ativa. Não usar cópias do Git como se fossem o estado atual. Criar
um diretório de **preparação** novo antes do comando. Nenhum argumento de saída
pode apontar para um registro ativo ou arquivo de entrada.

```text
python3 docs/station-android/r169/operator/prepare_all.py \
  --kit-zip <ZIP-R169-recebido-e-conferido> \
  --r168-receipt docs/station-android/r168/server-return.json \
  --catalog <catalogo-atual-exportado> \
  --profiles <perfis-atuais-exportados> \
  --modes <modos-atuais-exportados> \
  --manifest <motores-atuais-exportados> \
  --out-manifest <preparacao-nova>/manifest.json \
  --out-profiles <preparacao-nova>/profiles.json \
  --out-modes <preparacao-nova>/modes.json \
  --out-audit <preparacao-nova>/preflight.json
```

Confirmar `enginesAdded=4`, `profilesAdded=492`, `modesAdded=492`, prefixos
anteriores idênticos, 23 aprovações PSX/2P e `capcomVs2pProfileId` igual a
`psx-r169-20261015-005-versus`. Em seguida usar o mecanismo transacional e
as verificações de publicação já empregados na R168. Este preparador não
substitui essas etapas. Não publicar um conjunto se qualquer preflight falhar.

Após a publicação efetiva, emitir recibo `serverApplied=true` com hash do kit,
contagens, quatro IDs, versão do serviço e consulta pública **autenticada e
assinada** das capacidades da edição exata `station_767e5e92f2f353479e6120f1c28f96a7`:
conteúdo SHA-256 `6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73`,
PSX R169, VS, duas vagas. Só então executar a partida física entre os dois
telefones. `HTTP 200`, sala `playing` e simulações não provam imagem, áudio,
controles nem saída do anfitrião.
