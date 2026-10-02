# Handoff — continuar a Station no aparelho

Data: 02/10/2026. Base deste texto: `docs/licenca-teste-station-20261001` commit `289269c`. A porta `5192` já está no ar e a licença de teste já foi criada. Esta rodada não repete essa subida e não cria outra licença.

O app de teste já está instalado num Samsung. Falta um código de ativação ainda válido para o dono digitar no telefone. O código gravado em `RETORNO-LICENCA-TESTE-20261001.md` venceu em `2026-10-02T00:02:14Z`. Às `2026-10-02T00:08:10Z` esta branch ainda estava em `289269c`. Nem o Windows nem o telefone ativaram a licença.

## 1. O que já existe

### Servidor, medido no retorno `289269c` em 01/10/2026 20:48 -0300

- API Station em `127.0.0.1:5192`, unit `turborama-station-api.service`, DLL `/opt/turborama-station-20261001/TurboRamaSuiteOnlineServer.dll`, SHA-256 `862323d20072d5c461c2228f0af8ef04af1a276f0e743649520a312e67cd2226`, PID `86341` naquela hora.
- `GET 127.0.0.1:5192/health` HTTP 200. `GET 127.0.0.1:5192/ready/station` HTTP 200.
- Suite `5190` segue a DLL antiga SHA-256 `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`, PID `2943`. `POST /v1/suite/challenges` com `{}` respondeu HTTP 400 `JSON_INVALID`.
- Admin da `5191` não foi trocado. PID `2948` era o gateway de conteúdo. Unit `turborama-suite-admin.service` ficou active.
- PIX `5187` não foi alterado. PID `2940`. `GET /v1/health` HTTP 200.
- Migration `028_station_android` já aplicada. Não aplique de novo. O ledger também tem `027_suite_download_notifications`.
- Nginx público: `^~ /v1/station/` vai para `127.0.0.1:5192`. `^~ /v1/suite/` continua na `5190`. Base `https://app.lzgames.com.br`.
- keyId público Station `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`. A SPKI pública já está no retorno anterior. Não cole a chave privada.
- Sem sessão, as rotas públicas responderam: ativação, completar, desafio e sessão HTTP 400 `JSON_INVALID`; `GET /v1/station/me`, `GET /v1/station/catalog` e `POST /v1/station/downloads/authorize` HTTP 401 `STATION_SESSION_INVALID`.
- Contrato que permanece: depois de uma sessão válida, catálogo e download ficam 503. TTL do código 15 minutos, do desafio 60 segundos, da sessão 180 segundos. Não há heartbeat. Prefixo da licença Station `STA-`. Um id `TS-` da Suite não entra na Station.
- Licença já gravada: `STA-D7AE45616B415B2C7550315C0392C5D8`. Produto `TURBORAMA_STATION_ANDROID`. SKU `STATION_ANDROID_LIFETIME_1_DEVICE`. `9990` centavos BRL. Prazo `LIFETIME`, `expires_at` comercial nulo. Um aparelho. Nome `Teste Station`. Compra `station-teste-20261001`, item `android-1`, cliente `teste-station`, sistema `TURBOBOX_V1`. Estado na emissão `PENDING_ENROLLMENT`. `activation_consumed` falso. Banco, só o nome: `postgres`.

### App, medido neste PC depois desse retorno

- O login Station foi para o `classes8.dex` do APK TESTE. SHA-256 do APK `23ef25e50c1f2a3092b3074688195b1bafccde0191db250b64b5ef925545876d`, `1579476090` bytes. SHA-256 do `classes8.dex` `45bba907ed9616286d0be792d03539cbefbd5a1e65bdb14fadf50edc06232efb`. O `classes24.dex` da Vita permaneceu `a369a4c58db4fff6a338601495a07842c3148478110b0ac057893692e017d0ea`.
- `StationConfig.ENABLED` está ligado neste APK. A senha local continua valendo para qualquer texto que não seja um código Station. Campo vazio só usa a Station se o aparelho já tiver `station-license-id.txt`. Perfil 503 mostra `Bem-vindo`. O dex não chama catálogo nem download.
- Instalado com `adb install -r` no Samsung SM-A566E, Android 16, SDK 36, serial `RQCY30751WY`. Os dados do app foram mantidos. O app abriu em `ESActivity`, na tela `Seja Bem-Vindo`, porque a sessão local antiga ainda existia. Nenhum código Station foi digitado.
- O Cemu estável e o app 1.0 não foram trocados. Catálogo Sambox e HMAC de jogo ficam fora desta linha.

## 2. O que ainda falta

Isto não entra como construção nesta rodada. Confirme cada item no retorno com `sim`, `nao` ou `nao-observado`.

1. Código novo. O de 15 minutos venceu. A licença continua a mesma e o aparelho ainda não a consumiu. Esta é a única ação desta rodada.
2. Painel. `/admin` e `/admin/clientes/{licenseId}` não mostram a licença Android. O admin da `5191` não foi trocado.
3. Venda. O site e o TurboBox não vendem `STATION_ANDROID_LIFETIME_1_DEVICE` nem entregam o código na área do cliente.
4. Catálogo privado, cerca de 18 mil itens, e o adapter de gateway Station. `GET /v1/station/catalog` e `POST /v1/station/downloads/authorize` continuam fechados. `GET /v1/station/artifacts/{grantId}` não existe.
5. Prova no aparelho. A instalação não cria a chave do Android Keystore. Isso só acontece quando um código válido é digitado no telefone. Não ative daí para improvisar essa prova.
6. Operação ainda não feita: backup restaurável desta licença, prova dos papéis `turborama-suite` e `turborama-suite-admin`, compra sintética pelo site e plano de rollback da `028`.

## 3. Ação desta rodada

Não substitua `5190`, `5191`, PIX, Nginx nem systemd. Não aplique migration. Não abra catálogo nem download. Não crie segunda licença. Não ative. Não imprima pepper, DSN, token admin nem chave privada.

Na raiz deste checkout:

```bash
python3 docs/station-android/scripts/emitir-licenca-teste.py
```

O script já está no commit `289269c`. Se o código anterior ainda estiver válido, ele imprime `activationCode=JA_ATIVO_NAO_REIMPRIMIVEL` e não gera outro. Nesse caso, pare e escreva isso no retorno. Não copie um código que o script não imprimiu agora.

Se a licença ainda estiver `PENDING_ENROLLMENT`, `ACTIVE` e com `activation_consumed` falso, o script reemite o código na licença `STA-D7AE45616B415B2C7550315C0392C5D8`. Se ele sair com `licenca nao recebeu o codigo`, pare. A matrícula pode já ter sido concluída. Não invente outra compra.

Grave a saída na seção Licença de `RETORNO-CONTINUAR-STATION-20261002.md` e faça push nesta branch em seguida. O código vence em 15 minutos. O restante do retorno pode ir no mesmo commit. Não segure o código para medir catálogo ou painel.

## 4. Como preencher o retorno

Responda cada linha. Onde não mediu, escreva `nao-observado`. Não deixe o marcador `(preencher)`.

Para as rotas públicas, uma consulta sem sessão basta: status HTTP e código JSON. Não abra sessão e não conclua ativação.

No fim, commit e push de `docs/continuar-station-20261002`.
