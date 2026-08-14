# TurboRama Online — fronteira definitiva do servidor

O servidor TurboRama Online licencia e reconhece máquinas. Ele não é o provedor PIX, não controla os
preços locais do quiosque e não deve ser necessário para o funcionamento normal do EmulationStation.

## O servidor faz

- cadastro de Cliente, Licença e Máquina;
- associação da chave pública ao DeviceId;
- desafio de uso único e prova de posse da chave privada;
- perfis `TPM_BOUND`, `SOFTWARE_BOUND_ONLINE` e, futuramente, `USB_TOKEN_BOUND`;
- status `ACTIVE`, `SUSPENDED`, `REVOKED`, `MAINTENANCE` e `TRANSFER_PENDING`;
- registro de último contato, sessão e tentativa de clonagem;
- suspensão, revogação, transferência e nova autenticação declarativas.

## O servidor não faz

- não recebe nem altera a tabela de preços do TurboRama;
- não guarda o Access Token do estabelecimento;
- não cria nem consulta cobrança Mercado Pago;
- não entrega QR Code ao quiosque;
- não envia PowerShell, script, executável ou código arbitrário ao cliente.

## Pagamento

Preços, criação da cobrança, consulta da confirmação e concessão de créditos permanecem no conjunto
local TurboRama + agente PIX + provedor Mercado Pago/adaptador bancário.

## Indisponibilidade

Timeout, DNS, perda de internet, túnel indisponível e erro `5xx` preservam a última autorização local.
Somente uma recusa explícita e autenticada da licença pode bloquear novas cobranças PIX. Ela nunca
deve encerrar jogos, retirar créditos existentes, bloquear F10/F12 ou impedir o uso normal do
quiosque.

Sem internet, o provedor de pagamento também não consegue criar/confirmar uma nova cobrança; somente
essa compra fica temporariamente indisponível. O restante do sistema continua local.

## Regra de compatibilidade

`provider=online` é legado e incorreto. O provedor local deve ser `mercadopago` ou `adapter`; a
licença on-line é habilitada em campo separado. Ativação de licença preserva provedor, PDV, credencial
protegida e preços locais.

Qualquer endpoint antigo de preço/pagamento no servidor é legado e não deve ser chamado pelos
clientes atuais. Sua remoção pública precisa ocorrer em rodada Linux controlada, com handoff,
rollback e validação do site/túnel existentes.

## Transferência comprovada de hardware

Trocar placa-mãe pode manter a chave de software já protegida no perfil Windows, mas altera o
fingerprint de hardware. Esse caso deve continuar sendo recusado como `MACHINE_BINDING_MISMATCH`
até existir uma autorização administrativa explícita.

A transferência correta é atômica:

1. o administrador confirma novamente sua senha no painel;
2. o servidor coloca a licença em `TRANSFER_PENDING`, encerra sessões, suspende os vínculos antigos
   e emite um código de uso único;
3. o configurador local envia a prova da chave privada, o novo fingerprint e o código;
4. o servidor aceita tanto a mesma chave em hardware novo quanto uma chave nova criada após
   reinstalação;
5. somente após a prova válida a licença volta para `ACTIVE`, com exatamente uma máquina ativa;
6. os vínculos antigos permanecem suspensos na auditoria e não podem abrir sessão.

Gerar um código comum não autoriza mudança de fingerprint e não substitui esse fluxo.
