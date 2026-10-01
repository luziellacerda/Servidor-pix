# Handoff — licença de teste e o que ainda falta

Data: 01/10/2026. Este texto manda criar uma licença Station de teste e devolver, no arquivo `RETORNO-LICENCA-TESTE-20261001.md`, tudo o que o app precisa para a implementação. A porta `5192` já está no ar. Esta rodada não repete essa subida.

## 1. O que já está feito

- API Station isolada em `127.0.0.1:5192`, DLL `862323d20072d5c461c2228f0af8ef04af1a276f0e743649520a312e67cd2226`, unit `turborama-station-api.service`.
- Suite da `5190` permanece a DLL `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`. Não reiniciou na rodada anterior.
- Migration `028_station_android` aplicada. O ledger também tem `027_suite_download_notifications`.
- Nginx de `app.lzgames.com.br` tem `location ^~ /v1/station/` para a `5192`. `location ^~ /v1/suite/` continua na `5190`.
- Prova pública já feita de fora: ativação com código inexistente responde 403 `STATION_ACTIVATION_INVALID`. Sessão sem aparelho vinculado responde 403 `STATION_DEVICE_DENIED`.
- `GET /v1/station/me` sem bearer responde 401 JSON, não o HTML do portal.
- O app preparado tem a SPKI pública e o keyId `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`. `StationConfig.ENABLED` continua `false`. O APK TESTE instalado não foi trocado.
- Catálogo e download continuam fechados de propósito, com 503 depois de uma sessão válida.

## 2. Criar a licença de teste

Não substitua `turborama-suite-admin` da porta `5191`. Esse binário antigo não tem a rota Station, e o admin candidato escuta o socket de produção. Não suba outro admin nesse socket.

Não troque a DLL da `5190`. Não aplique outra migration. Não ative a licença. Quem digita o código é o app, numa rodada seguinte.

O banco é o mesmo que a API `5192` usa. O nome conhecido no estudo é `postgres`. Se for outro, use só esse nome na variável `STATION_TEST_DATABASE` e no retorno. Não escreva a connection string.

Pepper Station, já instalado: `/etc/turborama-suite/station-activation-pepper`. Não imprima o conteúdo.

```bash
python3 docs/station-android/scripts/emitir-licenca-teste.py
```

O script cria, se ainda não existir, uma entrega sintética:

- produto `TURBORAMA_STATION_ANDROID`
- SKU `STATION_ANDROID_LIFETIME_1_DEVICE`
- preço do contrato `9990` centavos, `BRL`, prazo `LIFETIME`, `expires_at` nulo
- um aparelho ativo
- compra `station-teste-20261001`, item `android-1`, sistema `TURBOBOX_V1`
- cliente `teste-station`, nome `Teste Station`
- licença `STA-` mais 32 hex, estado `PENDING_ENROLLMENT`
- um código Base64URL de 32 bytes, válido por 15 minutos

Se o código anterior ainda estiver válido, o script não gera outro e imprime `activationCode=JA_ATIVO_NAO_REIMPRIMIVEL`. Nesse caso, copie o código que já está neste retorno. Se o código venceu e a licença ainda não foi ativada, rode o script de novo: a mesma licença recebe outro código.

Copie a saída para a seção Licença do retorno e faça push na hora. Sem esse push, o código vence antes de chegar ao app.

## 3. O que ainda falta, e não entra nesta rodada

1. Painel visual. As páginas `/admin` e `/admin/clientes/{licenseId}` não chamam rota Station e não têm cartão Android. O admin da `5191` não foi trocado. Confirme isso no retorno, sem implementar a tela.
2. Venda no site. O TurboBox encontrado no estudo vende o SKU Windows, com outro preço. Não há checkout Android de R$ 99,90 nem entrega do código na área do cliente. Confirme se esse SKU existe no site. Não edite o site.
3. Catálogo privado e gateway. Há cerca de 18 mil itens Android por controlar. `GET /v1/station/catalog` e `POST /v1/station/downloads/authorize` ficam em 503. Não abra URL de jogo.
4. APK de teste. A flag do app só liga numa compilação de teste, depois que o código deste retorno existir. O APK de 1,58 GB instalado permanece.
5. Vetor no aparelho. A prova de fora usou chave .NET. Falta a mesma assinatura com a chave do Android Keystore num aparelho real.
6. Operação que ainda não foi feita: backup restaurável desta licença, teste de papel `turborama-suite` e `turborama-suite-admin`, compra sintética pelo site e plano de rollback da `028`. Não faça isso agora. Diga, no retorno, o que já existe e o que não existe.

## 4. Como preencher o retorno

Responda cada linha de `RETORNO-LICENCA-TESTE-20261001.md`. Onde a resposta for desconhecida, escreva `nao-observado`. Não deixe linha em branco.

Para as rotas, consulte sem sessão e anote só o status e o código JSON:

- `POST /v1/station/activations/challenge`
- `POST /v1/station/activations/complete`
- `POST /v1/station/challenges`
- `POST /v1/station/sessions`
- `GET /v1/station/me`
- `GET /v1/station/catalog`
- `POST /v1/station/downloads/authorize`

Não conclua a ativação. Não crie segunda licença. Não cole segredo além do código de teste e da SPKI pública.

No fim, commit e push desta branch.
