# Handoff — implementar o que ainda falta, sem gastar a licença

Data: 02/10/2026. Base: `docs/continuar-station-20261002` commit `861b666`. A porta `5192` está no ar. A licença de teste já existe. O app de teste já tem o login Station. Esta rodada não emite código, não ativa e não repete a subida da `5192`.

## 1. O que aconteceu no telefone

O código gravado em `RETORNO-CONTINUAR-STATION-20261002.md` venceu em `2026-10-02T00:34:52Z`. Não foi digitado. A licença não foi ativada daqui nem no aparelho.

- Aparelho: Samsung SM-A566E, Android 16, SDK 36, serial `RQCY30751WY`.
- O APK com o login Station foi instalado por cima, sem apagar dados. A sessão local antiga, arquivo `authenticated-session-v1.bin` em `no_backup`, fez o app abrir `ESActivity` direto. A tela de senha não apareceu.
- O pacote não é depurável para o `run-as`. O telefone não tem root. O arquivo da sessão não foi movido. `pm clear` não foi usado. Jogos e saves ficaram.
- Às 21:31 -0300 foi instalado um APK igual, com `android:debuggable` no manifesto. O `aapt dump badging` desse arquivo mostra `application-debuggable`. O Android do aparelho não marcou o pacote: `dumpsys` continuou sem `DEBUGGABLE` e o `run-as` respondeu `package not debuggable`.
- A volta do APK anterior falhou com `INSTALL_FAILED_INSUFFICIENT_STORAGE`. Havia cerca de 4,6 GB livres. O telefone ficou nesse APK das 21:31. Os dados continuam.

Conclusão para o servidor: outro código agora não entra no telefone. Não rode `emitir-licenca-teste.py` nesta rodada. Se o script for executado por engano e imprimir código, não cole esse código no retorno e não faça push dele.

## 2. O que já existe e não se refaz

- `5192` Station, migration `028` aplicada, nginx `^~ /v1/station/`, Suite `5190`, admin e PIX como no retorno `861b666`.
- Licença `STA-D7AE45616B415B2C7550315C0392C5D8`, produto `TURBORAMA_STATION_ANDROID`, SKU `STATION_ANDROID_LIFETIME_1_DEVICE`, 9990 centavos BRL, vitalícia, um aparelho, nome Teste Station, compra `station-teste-20261001`.
- No retorno `861b666` ela estava `PENDING_ENROLLMENT`, `activation_consumed` falso, sem segunda licença.
- Catálogo e download seguem fechados. O app instalado não chama essas rotas para abrir.
- keyId público `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`. Não cole a chave privada nem o pepper.

## 3. O que implementar nesta rodada

Só isto. Cada item tem uma ação. O que está marcado como não fazer, não faça.

1. Backup restaurável desta licença de teste. Faça. Grave só as linhas desta compra e desta licença. Arquivo fora do Git, modo `600`, dono root. No retorno: caminho, tamanho, modo e se o `licenseId` aparece. Não cole o arquivo, o verifier, o pepper nem a connection string. Não faça backup do banco inteiro de compradores.
2. Plano de rollback da `028`. Escreva no retorno, em passos curtos, o que seria revertido e o que não pode ser apagado porque a migration só alargou constraints. Não execute o rollback. Não aplique migration nova.
3. Papéis `turborama-suite` e `turborama-suite-admin`. Meça se existem e se um deles lê a licença Station. Não troque senha, grant nem dono. Não imprima senha.
4. Painel `/admin`. Não troque o binário da `5191` nem o socket dele. Se o cartão Android só existe trocando esse processo, pare e escreva isso. Não suba admin novo na porta de produção.
5. Site e TurboBox. Não edite o checkout ao vivo. Confirme se `STATION_ANDROID_LIFETIME_1_DEVICE` continua ausente. Não faça compra sintética no site.
6. Catálogo, gateway e `GET /v1/station/artifacts/{grantId}`. Não implemente e não abra. Continuam fechados. Não gere URL de jogo.
7. Código novo e ativação. Não emita. Não ative. Não crie segunda licença. A prova do Android Keystore continua no telefone, e o telefone ainda não chega na tela do código.

Não reinicie `5190`, admin, PIX nem a `5192`, salvo se o backup exigir uma sessão já aberta do Postgres. Nesse caso use `sudo -u postgres` como o script de licença, sem restart de serviço.

## 4. Como preencher o retorno

Responda cada linha de `RETORNO-IMPLEMENTAR-FALTAS-STATION-20261002.md`. Onde não mediu, escreva `nao-observado`. Não deixe `(preencher)`.

Não cole segredo. Não cole código de ativação, mesmo que o script imprima um.

No fim, commit e push de `docs/implementar-faltas-station-20261002`.
