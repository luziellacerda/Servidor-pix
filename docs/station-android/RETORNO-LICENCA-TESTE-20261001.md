# Retorno da licença de teste e do que falta

Preencha tudo. Faça push nesta branch logo depois de emitir o código. O código vence em 15 minutos. Não ative a licença daí. Não cole pepper, DSN, token admin, chave privada nem URL de jogo.

## Licença

- parou porque:
- banco usado, só o nome:
- licenseId:
- activationCode:
- expiresAt UTC:
- displayName:
- sourceSystem:
- sourcePurchaseId:
- sourceItemKey:
- customerRef:
- productId:
- sku:
- amountCents:
- currency:
- licenseTerm:
- expires_at da licença comercial:
- aparelhos ativos permitidos:
- enrollment_state depois da emissão:
- código já estava ativo e não foi reimpresso:

## Processo que ficou no ar

- PID e hash da DLL 5190:
- PID e hash da DLL 5192:
- admin 5191 foi substituído:
- PIX 5187 foi alterado:
- `GET 127.0.0.1:5192/health`:
- `GET 127.0.0.1:5192/ready/station`:
- `POST 127.0.0.1:5190/v1/suite/challenges` com `{}`:
- keyId Station:
- spkiBase64Url:

## O que o app precisa para implementar

- URL pública:
- rotas que existem e o status sem sessão, uma por linha:
- catálogo continua 503:
- download continua 503:
- TTL do código em minutos:
- TTL do desafio em segundos:
- TTL da sessão em segundos:
- existe heartbeat:
- prefixo do id da licença Station:
- um id `TS-` da Suite entra na Station:
- nome mostrado quando o perfil existe:
- nome quando o perfil responde 503:
- o painel visual em `/admin` mostra esta licença:
- o site ou TurboBox já vende `STATION_ANDROID_LIFETIME_1_DEVICE`:
- onde o código seria entregue ao comprador real:
- o que ainda não existe no servidor, em lista:
- o que quebra se o app ligar a flag hoje sem código:
