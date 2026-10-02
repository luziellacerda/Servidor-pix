# Retorno — senha Station de 48 horas

Preencha e faça push nesta branch logo depois de emitir a senha de teste. Não cole pepper, DSN, token, chave privada nem URL de jogo. Não ative a licença. Não envie WhatsApp desta licença de teste.

## Prazos

- parou porque: concluiu
- primeira senha de compra Station, em horas: 48
- reemissão humana, em minutos: 30
- reemissão humana substitui senha ainda válida: sim
- OTP de 15 minutos da Suite Windows foi alterado: nao
- processo reiniciado, só se o emissor mudou: turborama-station-issue-admin.service (PID 110649, helper station-issue-admin/3)
- 5190 reiniciada: nao (PID 2943)
- 5192 reiniciada: nao (PID 86341)
- admin ou gateway reiniciado: nao (gateway PID 2948)
- PIX reiniciado: nao (PID 2940)

## Senha de teste

- licenseId: `STA-D7AE45616B415B2C7550315C0392C5D8`
- activationCode: `uHCqbtKKXuWS1afy15ksvwYOh9AxhOe2TspfNZt9wGo`
- expiresAt UTC: `2026-10-04T01:10:16Z`
- prazo aplicado nesta emissão, em horas: 48
- enrollment_state depois: PENDING_ENROLLMENT
- activation_consumed: false
- segunda licença criada: nao
- licença ativada desta máquina: nao
- WhatsApp desta licença enviado: nao-enviado

## WhatsApp no código

- compra paga tem mensagem de 48 horas: sim (tipo `station_issued_first`, produto, pedido, valor, licença, senha, prazo 48h e aviso da reemissão de 30 min; cadastro `teste-station` é pulado)
- reemissão humana tem mensagem de 30 minutos, só com confirmação: sim (checkbox `enviar_whatsapp` em `/admin/station`)
- ativação tem mensagem sem repetir a senha: sim (worker `turbobox-notifications` no estado `BOUND`/`activation_consumed`)
- rotas de PIX ou extração foram alteradas: nao
