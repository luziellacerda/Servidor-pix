# Recuperação da versão estável TurboStations R143

## Voltar ao ponto aprovado

Use a tag `estavel-station-r143-20261013` e o manifesto desta pasta. Ela aponta o commit documental final; a fonte executável do APK continua `26257869b59af8ecea44932ab287705990a9acbe`. Não escolher APK por data, nome “latest”, branch padrão antiga ou preparo R143 sem sufixo final2.

O backup completo do ponto é `F:/TurboStations-Backup-Completo-20261011/desenvolvimento/reconstrucao-atual/estavel-station-r143-20261013`. Verifique `backup-receipt.json` e `INPUT-MAP-R143.json` privados. A opção preferencial é recuperar o APK já assinado, SHA256 `508480e3671ae924f037bd32f6d71efda7a9d905c45c7b2571bd9f219add83d4`, 2.715.024.399 bytes, e instalar por atualização no aparelho autorizado ocioso.

## Recuperar o Git sem internet

Os bundles completos `git/app.bundle` e `git/server.bundle` incluem história alcançável, ramo e tag deste checkpoint, sem dependência dos antigos bundles incrementais. O backup valida ausência de pré-requisitos e testa a restauração da tag. Em uma pasta NOVA, usando Git do backup:

```powershell
git clone --branch estavel-station-r143-20261013 CAMINHO_DO_BACKUP/git/app.bundle TurboStations-R143-recuperado
git -C TurboStations-R143-recuperado show estavel-station-r143-20261013:versions/estavel-station-r143-20261013/MANIFESTO-ESTAVEL.json
git clone --branch estavel-station-r143-20261013 CAMINHO_DO_BACKUP/git/server.bundle Servidor-R143-handoff-recuperado
```

O bundle do servidor recupera fonte e handoff; não altera o binário Linux em execução. Não executar scripts administrativos antigos de aplicação para simular rollback. Verificar o recibo R141 aplicado e preservar licenças/cadastros anteriores.

## Recuperar o APK no telefone

1. Confira tamanho, SHA256, assinatura v2/v3 e certificado `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` do APK recuperado. Os scripts/manifesto em `installer/` selecionam explicitamente esses bytes.
2. Copie/ajuste apenas os caminhos locais `apkPath` e `adbPath` no manifesto de uma cópia de trabalho se o backup mudou de unidade. Preserve revisão, SHA, bytes e certificado. Encerre partida/download pelo app e conecte um telefone por vez, desbloqueado e com depuração autorizada.
3. Antes da atualização, registre UID, firstInstallTime e dataDir por leitura de dumpsys package do próprio app. Execute o BAT original. Ele confere o hash do APK, USB, foreground e ausência de sessão online; a conclusão/cancelamento dos downloads é confirmada pelo operador. A atualização pede preservação de dados e o Android verifica a compatibilidade da assinatura. Não contornar recusa do instalador ou do Android.
4. Confira o hash integral instalado e o recibo do BAT. Leia dumpsys package novamente e compare UID, dataDir e firstInstallTime aos registros anteriores. Essas três comparações são separadas: o recibo do BAT não as coleta automaticamente. Abra pela entrada oficial e preserve saves/login/licença.
5. Repita Top Gear 3000 VS três participantes e teste Bomberman no modo compatível. Quatro/cinco telefones e perda de sinal têm ensaios próprios.

Se Android recusar retorno de uma versão futura por versão/assinatura incompatível, parar e analisar a recusa preservando dados; não desinstalar para contornar. O BAT original tem SHA `af9b568ebaf14bd428dac1bc98ee3e6feea453fe58a3c74e52d97a2881247172`, PS1 `10d930863253e7ea04d7a1576ecb3856088a7933cb40e0385246d0d71c268fc3`.

## Reconstruir somente quando o APK congelado não servir

A fonte ativa é `integration/snes-multitap-unique-flag-r143-20261013-final2`, commit `26257869b59af8ecea44932ab287705990a9acbe`. Donor obrigatório: APK R142 SHA `f99b51a04f4967c086533157835868dcc6a22a3eb06eb63706886f9f60453b83`, incluído em `apks/`. A R142 é base de montagem, não versão estável de gameplay. Algumas mensagens herdadas dizem R139; o guard BASE efetivo exige R142. Não modificar fontes congeladas para ajustar essas mensagens.

Ferramentas privadas: JDK17, apktool 3.0.3 exato, NDK r28c, Android34, build-tools35, Python e Git. Use os arquivos/pins do mapa privado. Saídas novas fora do checkout, com espaço livre, sem sobrescrever assembly/recibos originais. A pasta `reconstruction/` conserva saídas originais; as outras preparações R143 são históricas recusadas.

Na raiz do checkout recuperado, ajuste as variáveis de caminho aos arquivos conferidos:

```powershell
$stationRecipe = 'integration/snes-multitap-unique-flag-r143-20261013-final2/recipe'
$stationSource = '26257869b59af8ecea44932ab287705990a9acbe'
$stationBase = 'CAMINHO_DO_BACKUP/apks/TurboStations-Premium-R142-multitap-snes-20261013.apk'
$stationTools = 'CAMINHO_DO_BACKUP/tools'
$stationBuild = 'DIRETORIO_NOVO_COM_ESPACO'
python -B "$stationRecipe/build_dex.py" --base $stationBase --jdk-bin "$stationTools/jdk17/bin" --apktool "$stationTools/apktool_3.0.3.jar" --resolver-receipt "$stationRecipe/../evidence/host-control-reader.json" --source-commit $stationSource --out "$stationBuild/dex"
python -B "$stationRecipe/build_identity.py" --base $stationBase --dex "$stationBuild/dex/classes35.dex" --dex-receipt "$stationBuild/dex/dex-compilation.json" --ndk "$stationTools/ndk-r28c" --source-commit $stationSource --out "$stationBuild/identity"
python -B "$stationRecipe/assemble_apk.py" --base $stationBase --dex "$stationBuild/dex/classes35.dex" --dex-receipt "$stationBuild/dex/dex-compilation.json" --identity "$stationBuild/identity" --source-commit $stationSource --out "$stationBuild/assembly"
```

Alinhe com zipalign `-P 16 4`; assine v2/v3 com a chave original privada de `private-signing/`, usando senha em variável de ambiente. Consulte o procedimento privado preservado e o README de assinatura no backup; jamais publicar credenciais. O guard de certificado original permanece obrigatório. Depois:

```powershell
python -B "$stationRecipe/verify_apk.py" --apk APK_ASSINADO_NOVO --receipt "$stationBuild/assembly/unsigned-package.json" --java "$stationTools/jdk17/bin/java.exe" --apksigner "$stationTools/android-build-tools-35/lib/apksigner.jar" --zipalign "$stationTools/android-build-tools-35/zipalign.exe" --out RECIBO_NOVO
```

Exigir fonte congelada, método inverso, cinco trocas, zero adições/remoções, 13.855 entradas preservadas, 39 DEX exatos e core/runtime originais. Uma reconstrução futura não deve presumir o SHA do APK congelado: verificar seus novos bytes, assinatura, vínculos e comparar escopo antes de promover/instalar. Helpers privados one-shot recusam saídas existentes; não executá-los novamente sobre os diretórios desta entrega.

`tooling/release/compile_current.py` e `install_current.py` têm resolvedor até R134. CURRENT apontar R143 não amplia esse programa. Eles permanecem intactos; usar a receita R143 e o BAT explícito acima. Não introduzir fallback R128/R134 para forçar aceitação.

## Bases anteriores e servidor

| Revisão | Fonte executável | SHA256 do APK |
| --- | --- | --- |
| R139 | `42da152e54bbbb4a6d569ca26fe90835503bcccb` | `19acdeceef86bc66c2475300cbd142da783850c02f5a9e2068d8652ef8523164` |
| R140 | `b93623b708bafe151441d3e4920acad51718e118` | `61e873e7bbd75a13fbd0d55515a3726f4debd8984cc0fe8f9d73401e0897792c` |
| R141 | `240b7c0504252e542975089f585bd67d5264f074` | `b5a76023860bceb2aa11c946c8003fa2d92951e700dbde3a498f8316857670d4` |
| R142 | `9a38881f467ee533b4f8bcff3ea6769b98401175` | `f99b51a04f4967c086533157835868dcc6a22a3eb06eb63706886f9f60453b83` |
| R143 | `26257869b59af8ecea44932ab287705990a9acbe` | `508480e3671ae924f037bd32f6d71efda7a9d905c45c7b2571bd9f219add83d4` |

A R143 se reconstrói diretamente do donor R142 incluído. As bases anteriores, terceiros e checkpoints permanecem no backup único, com caminhos/hashes nos seus mapas; não duplicar todo o backup antigo nem apagar versões intermediárias. Git completo contém os overlays e a descrição histórica.

SNES usa cadastro existente. O kit R143 privado dos quatro owners independentes está incluído em `server-kit/` e aguarda aplicação; manter o operador atual de renovação da lista fixa, as 78 modalidades e os cadastros anteriores. Não reaplicar kit R141 nem supor cadastro R143 confirmado pelo recibo R141. Restaurar APK/fonte não restaura banco/licenças/saves e não autoriza deploy.
