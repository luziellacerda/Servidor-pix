# Handoff de correção — WhatsApp por observador isolado

Data: 2026-09-03  
Destino: servidor de produção TurboRama/TurboBox

## Esta ordem substitui a anterior

Este documento **cancela e substitui** o arquivo
`HANDOFF-INTEGRACAO-WHATSAPP-SESSION-OPEN-2026-09-03.md`.

Não implementar chamadas de WhatsApp dentro de `SuiteService`, `Store`, rotas de
ativação ou rotas de sessão. Não alterar o protocolo do EXE.

## Resultado solicitado

Criar no servidor um observador isolado que:

1. perceba uma licença que acabou de ser ativada com sucesso;
2. perceba uma nova sessão `session.open` que já foi autorizada;
3. localize o cliente e o telefone exclusivamente pelos dados da venda no
   servidor;
4. coloque a mensagem na fila de WhatsApp já existente do TurboBox/MenuIA.

O observador atua **depois** do resultado confirmado. Ele não participa da
autorização, não muda o resultado do login e não responde ao EXE.

## Limites obrigatórios

- Não modificar `src/TurboRamaSuiteOnlineServer/SuiteService.cs`.
- Não modificar `src/TurboRamaSuiteOnlineServer/Store.cs`.
- Não modificar criptografia, TLS, OTP, machine proof, placa-mãe, vínculo de
  dispositivo, licença, sessão, catálogo ou downloads.
- Não criar chamada do EXE para WhatsApp.
- Não aceitar telefone, nome ou e-mail enviados pelo EXE.
- Não enviar mensagem de download concluído. Essa função foi cancelada.
- Não enviar em `session.heartbeat`.
- Uma falha de WhatsApp jamais pode bloquear ou alterar licença/login.
- Não registrar token, telefone completo, serial, UUID ou chaves em logs.

## Fonte dos eventos — somente leitura

Usar os registros que o servidor já grava após o sucesso:

### Ativação concluída

Observar uma nova linha válida de `suite.suite_activation_completions`, ligada a
um challenge `device.activate`. Antes de notificar, confirmar por leitura que:

- `suite_licenses.product_id = 'TURBORAMA_SUITE'`;
- licença está `ACTIVE`;
- `activation_consumed = true`;
- `enrollment_state = 'BOUND'`;
- dispositivo vinculado está `ACTIVE`.

Chave idempotente recomendada:

`license.activated:{license_id}:{activation_generation}`

### Acesso autorizado

Observar um challenge consumido com `action = 'session.open'`. Antes de
notificar, confirmar por leitura que existe a sessão correspondente, com o mesmo
`license_id`, `device_id` e `session_id`, e que a licença/dispositivo continuam
ativos.

Chave idempotente recomendada:

`session.open:{license_id}:{session_id}`

O heartbeat mantém o mesmo `session_id` e nunca gera evento.

## Isolamento e estado do observador

- Executar como serviço/worker separado do serviço online de licenciamento.
- A credencial do observador no banco Suite deve ser somente leitura.
- Guardar cursor e chaves idempotentes em armazenamento próprio do observador ou
  no subsistema de notificações do TurboBox, nunca alterando as tabelas centrais
  de licença/sessão.
- Na primeira execução, registrar um marco inicial e ignorar eventos históricos
  anteriores a esse marco, evitando mensagens retroativas para clientes.
- Só avançar o cursor depois que a fila aceitar o evento; em falha temporária,
  repetir com atraso.
- A mesma chave idempotente deve produzir no máximo um item na fila.

## Resolução segura do destinatário

O caminho deve ser exclusivamente servidor a servidor:

`license_id -> suite_license_deliveries -> TURBOBOX_V1/source_purchase_id -> compra/cliente -> telefone`

Regras:

- aceitar apenas `source_system = 'TURBOBOX_V1'`;
- validar `source_purchase_id` como inteiro decimal canônico antes da consulta;
- exigir compra real associada à licença;
- normalizar e validar o telefone com a rotina existente do TurboBox;
- se não houver destinatário inequívoco, marcar como não enviável e registrar
  somente um código técnico sem PII;
- nunca escolher número por aproximação e nunca usar número de teste fixo em
  produção.

## Mensagens

Usar horário oficial do servidor em `America/Fortaleza`/UTC-3 e protocolo
derivado do identificador do evento, sem exibir identificadores completos.

### Licença ativada

```text
🟢 *TURBORAMA SUITE — LICENÇA ATIVADA*

Olá, {primeiro_nome}.
✅ Sua licença foi ativada com sucesso neste computador.

📅 Data: {dd/MM/yyyy}
🕒 Hora: {HH:mm:ss} (UTC-3)
🔐 Licença: {licenca_mascarada}
💻 Dispositivo: {dispositivo_mascarado}
🧾 Protocolo: {protocolo}

Guarde esta mensagem como confirmação. Se você não reconhece esta ativação,
entre em contato com o suporte oficial.
```

### Acesso autorizado

```text
🎮 *TURBORAMA SUITE — ACESSO CONFIRMADO*

Olá, {primeiro_nome}.
✅ Uma nova sessão foi autorizada para sua licença.

📅 Data: {dd/MM/yyyy}
🕒 Hora: {HH:mm:ss} (UTC-3)
🔐 Licença: {licenca_mascarada}
💻 Dispositivo: {dispositivo_mascarado}
🧾 Protocolo: {protocolo}

Se foi você, nenhuma ação é necessária. Se não reconhece este acesso, fale
imediatamente com o suporte oficial.
```

Não declarar que catálogo, downloads ou todos os serviços estão saudáveis: o
evento confirma somente ativação ou autorização de acesso.

## Critérios de aceite obrigatórios

1. O diff de `SuiteService.cs`, `Store.cs` e do protocolo do cliente deve ser
   vazio.
2. Ativação real bem-sucedida gera exatamente uma mensagem.
3. Repetir a mesma ativação não duplica mensagem.
4. Um novo `session.open` autorizado gera exatamente uma mensagem.
5. Repetir o mesmo `sessionId` não duplica mensagem.
6. Dez heartbeats seguidos geram zero mensagens.
7. Licença negada ou sessão negada gera zero mensagens.
8. Telefone ausente/ambíguo gera zero envio e nenhum destinatário alternativo.
9. MenuIA indisponível não altera HTTP, asserção ou estado de licença/sessão;
   fica apenas como retry do observador.
10. Busca automática no repositório confirma ausência de mensagem/evento de
    download.
11. Logs e Git não contêm tokens, telefone integral, serial, UUID ou chave.

## Implantação e retorno exigido

Implantar somente o worker isolado e sua configuração, sem reiniciar nem trocar
o binário do serviço online de licenciamento se isso não for necessário.

No retorno, informar em um único documento:

- commit e arquivos realmente alterados;
- serviço criado/alterado e usuário Linux utilizado;
- fonte exata dos dois eventos observados;
- mecanismo de cursor/idempotência;
- como o telefone foi resolvido;
- comandos e resultados dos 11 testes acima;
- evidência de que `SuiteService.cs`, `Store.cs`, EXE e downloads não mudaram;
- procedimento de rollback do observador.

Não responder com novo plano ou outro handoff. Executar, testar e devolver as
evidências finais.
