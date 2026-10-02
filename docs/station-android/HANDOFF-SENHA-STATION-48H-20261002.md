# Handoff — senha Station de 48 horas e reemissão de 30 minutos

Data: 02/10/2026. Base: `docs/implementar-faltas-station-20261002` commit `c295cd5`. O retorno daquela branch descreveu o uso e disse que o TTL não foi mudado. Esta rodada implementa esse uso.

A `5192` já aceita o prazo que estiver em `activation_expires_at`. Não é preciso reiniciar a `5192` para um prazo maior passar a valer. Não reinicie `5190`, admin, gateway nem PIX.

## 1. Prazos

Hoje `StationCommerceEndpoints.IssueAsync` grava `interval '15 minutes'` e a reemissão do admin chama esse mesmo método. O OTP da Suite Windows em `CommerceEndpoints.cs` também usa 15 minutos. Esse da Suite fica como está.

Mude só a Station:

- Primeira senha de uma compra Station paga: 48 horas. É a emissão do comércio, não o botão humano.
- Botão humano em `/admin/station`, rota de issue do helper `127.0.0.1:5194`: 30 minutos. A senha anterior deixa de valer na hora, mesmo que ainda não tenha vencido. Hoje o código recusa com `STATION_CODE_ALREADY_ACTIVE` enquanto a senha vale. No caminho humano, substitua o verifier em vez de recusar.
- Não altere o prazo da sessão (180 segundos), do desafio (60 segundos) nem o OTP da Suite.

Se o helper `5194` tiver SQL próprio com 15 minutos, mude esse SQL. Reinicie só `turborama-station-issue-admin.service` se o binário dele mudou. Não troque o socket do admin nem a porta `5191`.

## 2. WhatsApp

Implemente o envio, sem alterar rotas de PIX nem os avisos de extração e download da Suite.

- Compra paga, primeira senha: WhatsApp no número do cadastro. Texto com produto, pedido, valor, licença, senha, prazo de 48 horas e o aviso de que a loja pode gerar outra senha de 30 minutos. A senha não entra em log.
- Reemissão humana: a mesma forma, prazo de 30 minutos, só se o operador confirmar o envio.
- Aparelho ativou (`BOUND` e `activation_consumed`): mensagem de licença liberada neste aparelho, senha já usada, suporte pela loja. Sem repetir a senha.

Nesta licença de teste, não envie WhatsApp. O cadastro é `teste-station`. Se não houver um número de teste já gravado nessa compra, pule o envio e escreva `nao-enviado`. Não use número de outro cliente.

## 3. Uma senha de 48 horas para o teste

A licença `STA-D7AE45616B415B2C7550315C0392C5D8` continua `PENDING_ENROLLMENT` e nenhum código foi digitado no telefone. Os códigos de 15 minutos venceram sem uso. Esta entrega conta como a primeira senha do cliente: 48 horas, não 30.

Não use o script de 15 minutos. Não crie segunda licença. Não ative. Não abra catálogo nem download.

Grave a senha só em `RETORNO-SENHA-STATION-48H-20261002.md` e faça push nesta branch em seguida. O app no Samsung passa a abrir o campo quando recebe o extra `stationLogin`, sem apagar a sessão local. A senha de 48 horas espera esse uso.

## 4. O que não fazer

- Não reinicie `5190`, `5192`, gateway `5191` nem PIX.
- Não substitua o admin unix.
- Não aplique migration.
- Não mude o OTP de 15 minutos da Suite Windows.
- Não cole pepper, DSN, token, chave privada nem URL de jogo.
- Não mande WhatsApp desta licença de teste.
