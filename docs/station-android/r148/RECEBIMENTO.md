# Recebimento do kit privado R148

A R148 é candidata. O APK assinado correspondente tem SHA-256
`c8b8c30cac7d36ec2a8ca8d470542096919435c749dede535cd0d58648e00dc1`.
Receber somente `Kit-Servidor-TurboStations-R148-Independentes.zip`, de
12.180.697 bytes e SHA-256
`e94e2dcc7436c310da6fac6a8d0ab366f496e788146fa3d8ce28e062e244ce19`.
O arquivo privado está no pacote de entrega local, fora do Git. O kit R147
não cadastra os quatro IDs da R148.

Antes do registro, obter uma leitura protegida atual do catálogo, dos perfis,
dos modos e dos motores. Conferir o manifesto e os 38 arquivos do ZIP: seis
ELF, vinte dependências, quatro IDs R148 e os mesmos 78 modos revisados
(66 N64, nove GameCube e três Wii). PS1 continua sem nova aprovação HLE.
Usar a função atual `prepare_independent_owners.prepare`, respeitando sua
assinatura real; a mesclagem interna já fixa
`migrate_existing_native_profiles=False`. Aplicar pelo registrador
transacional vigente, de forma aditiva e sob `scan.lock`. Preservar IDs,
licenças, dados, perfis e modos anteriores. Não executar o `main` de
operadores históricos, reaplicar R141/QoS nem implantar ou reiniciar a API
para receber este kit.

Devolver recibo de aplicação e capacidades assinadas com os quatro IDs
exatos de `STATUS.json`. A instalação da R148 no Samsung A56 foi comprovada
por SHA integral no aparelho; ver o [recibo sanitizado](evidence/installation-a56-sanitized.json).
Ainda não há recibo de aplicação deste kit nem partida R148 validada. A divisão
de banda é independente deste kit; ver [diagnóstico dos downloads](../network-qos/DIAGNOSTICO-DOWNLOADS-ANTES-DEPOIS-20261014.md).
