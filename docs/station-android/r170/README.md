# R170 compilada neste Linux a partir do backup recebido

**Candidata assinada, sem instalação, cadastro dos novos motores ou implantação.**
A fonte produtiva congelada é `fb80831d79d2f10132454ed0751befdb3e38cbfe`. O retorno anterior PSX `ec2c693`
e o atualizador `3771f29` estão efetivamente dentro deste APK, preservando XML/CAS.
Não remontar a R169, executar seus operadores ou pedir novamente os kits recebidos.

## Arquivos conferidos

- APK: SHA-256 `42533ba2b53532be38872bcb97a07b0647e5a9b6091b565a1af7292f390be1c3`, 2,714,221,666 bytes; mesmo pacote e certificado original,
  assinaturas v2/v3 e alinhamento16KiB conferidos. Versão interna12, SDK26/34.
- Kit privado: SHA-256 `c559c5e8775e86f82e5f1e192cbcc42952d4c1de2ef180a1f38962d628909e42`, 15,026,006 bytes,58 arquivos/27ELF reais do APK.
  O kit não contém APK, ROM, BIOS, chave, senha, serial ou logs brutos.
- Backup: dois bundles completos e11 insumos críticos conferidos, incluindo R168,
  R169, autoridade original e kit. A auditoria dos43 APKs históricos é da produção;
  ela não foi repetida neste Linux. Arquivos tar/7z interrompidos foram excluídos
  dos insumos e preservados no disco.
- Autoridade privada: `station-build-r170-20261017/final/final-apk-proof.json`,
  `private-server-kit/kit-receipt.json` e `delivery-completed.json`.
  APK único em `final/TurboStations-Premium-R170.apk`; kit em `private-server-kit/`.

## Mudança efetiva e provas

Exatamente sete entradas substituídas;13.854 entradas e38 partições DEX idênticas
à R169. Preservadas803 classes externas ao delta nas duas partições alteradas;
51.632 classes únicas nas40 partições e969 referências internas conferidas.
A chamada CAS inválida da R168 foi rejeitada no teste negativo com seu DEX real.
Os demais cores/controles/opções, visual, downloads, ROMs, saves e dados permanecem.

O PSX contém retomada corrigida e envio despertado por evento, substituindo a
espera25ms apenas nesse endpoint. Novo JNI e método nativo foram vinculados;
nenhum export anterior foi removido. Dependências/SONAME e alinhamento16KiB
passaram; N64/Dolphin mudam somente seus marcadores de identidade, com exports
idênticos. As oito fontes PSX de `ec2c693` foram preservadas byte a byte.

O atualizador possui aviso por toque, download privado com sessão/link temporário,
SHA durante recebimento e PackageInstaller, mantendo a confirmação do Android.
A base real declarou SDK26; foram corrigidas as dependências28 da implementação
preliminar, incluindo certificado, versão e identificação do processo. SDKs e
fábrica originais foram preservados. API26/27 não foram testadas fisicamente.

Passaram45 verificações Java do API real/arquivos,85 do backend com assinaturas
reais e store sintético,13 do publicador com APKs/chaves descartáveis e8 de receita.
Nenhuma fixture entrou no APK; a superfície do painel veio dos fontes produtivos.
Essas provas, a assinatura e o transporte anterior não homologam gameplay Android.

## IDs exatos para cadastro aditivo

- **psx**: `pcsx-rearmed-ff81ed17-player-v3-9fb3e1531246`.
- **n64**: `mupen64plus-ae-33bf7021-n64-v3-18ac41c7a808`.
- **gamecube**: `dolphin-5102a033-gamecube-v3-7809f15fe05c`.
- **wii**: `dolphin-5102a033-wii-v3-7809f15fe05c`.

O kit inclui `incoming.json`, `engines-apk.json`, `profile-policies.json`, os27ELF,
provas finais, hashes individuais e referências R169. O montador portátil está em
`integration/psx-updater-r170/signing/package_server_kit.py`; a receita do APK está
em `integration/psx-updater-r170/recipe/build.py` e `signing/sign.py`.

## Próximas etapas, em ordem

1. Obter autoridade protegida fresca da R169. Cadastrar quatro IDs e requalificar
   as492 decisões existentes pelas tuplas completas, conservando recusas e cada
   registro antigo. Não aprovar pelo título nem ampliar controles/capacidade.
   Respeitar17MiB de perfis,9MiB de modos,1MiB de manifesto e256 owners; conferir
   os tamanhos reais antes de gravar. Não usar rollback antigo de8MiB.
2. Conferir capabilities autenticadas dos IDs novos. O serviço continua R169,
   PID238611/NRestarts0. A correção SDK26 do backend/publicador está somente em
   fonte; não houve deploy, restart, alteração de banco, rede100/800 ou outros
   produtos. Caso haja mudança de DLL, preparar sombra/rollback atuais e aguardar
   ausência de salas pelo procedimento vigente.
3. Validar o instalador manual no Windows. A cópia `installer-windows/` usa o
   bootstrap original e aponta por caminho relativo para o APK único; PowerShell
   e EXE não foram executados neste Linux, nem o instalador permanente foi trocado.
   Preservar essa distinção. Não instalar por ADB automaticamente nem limpar dados.
4. Instalar manualmente o mesmo APK nos dois aparelhos, conferir seus hashes e
   testar Capcom vs. SNK/VS com ambos os anfitriões. Observar quadro/som/controles,
   saída e retorno; medir progresso nativo/áudio e espera do envio. Não deduzir60FPS
   da SurfaceFlinger nem reutilizar medidas da R169 como resultado da correção.
5. Após o piloto, ativar as rotas de atualização e publicar somente APK aprovado
   no diretório privado pelo publicador, com certificado e registros efetivos.
   Não existe APK publicado nem `current.json` ativo desta entrega. A primeira
   adoção é manual, pois R169 não possui atualizador; sucessora de R170 precisa
   `versionCode` maior que12. Não elevar SDK para contornar validação.

Produção, licenças, banco, banda, sessões e celulares não foram alterados por
esta análise/montagem. A referência estável anterior permanece disponível.
