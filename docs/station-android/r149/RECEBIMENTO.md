# Recebimento do kit privado R149

**Pacote preparado; ainda não aplicado.** A fonte da receita é
`ef5d00b189a4a57bb2044e297b2f1b9b4fa90ce0` e a base assinada R148 tem
SHA-256 `c8b8c30cac7d36ec2a8ca8d470542096919435c749dede535cd0d58648e00dc1`.
O APK assinado R149 tem **2.715.089.935 bytes**, SHA-256
`698e8a7914cc7be2facc00e6701ff1f3533fa13f763358b51578a35d25c2741e`.
Receber somente `Kit-Servidor-TurboStations-R149-Independentes.zip`, de
**12.181.272 bytes**, SHA-256
`ed95ba61c92285937014a91e86b1dd7dd0a6928e10a9d063132973244e4eb5ec`.
Os hashes e tamanhos foram conferidos no backup privado local; o servidor
ainda precisa receber/verificar seus próprios bytes. O kit R148 segue sem
aplicação comprovada e não substitui a R149.

O reparo de `onPause()` em DEX10 pode ser confirmado **offline** no telefone
sem ação neste servidor. O teste **online** exige o kit/IDs correspondentes ao
APK R149 realmente instalado. Quando o pacote final chegar:

1. Autenticar localmente e obter baseline protegido **atual** de catálogo,
   perfis, modos e motores antes de escrever; preservar retorno/rollback.
2. Conferir SHA/tamanho do ZIP e manifesto (38 arquivos, sendo 37 listados),
   os seis ELF e vinte dependências reais, os quatro IDs exatos abaixo e o
   ledger revisado de 78 modos
   (66 N64, nove GameCube, três Wii). Nenhuma aprovação PS1 HLE nova.
3. Usar `prepare_independent_owners.prepare` com sua assinatura vigente; sua
   mesclagem interna já fixa `migrate_existing_native_profiles=False`.
   Registrar **aditivamente** pelo operador transacional vigente sob
   `scan.lock`, sem substituir perfis, licenças, dados, modos e IDs anteriores.
4. Devolver recibo aplicado e capabilities assinadas com os **quatro IDs R149
   exatos**. Só então testar sala, boot, controles distintos, perda de sinal e
   saída com participantes no mesmo APK R149.

Não executar `main` de operadores antigos, reaplicar R141/QoS, fazer migração
genérica, deploy ou restart da API para este cadastro. A divisão de banda é
assunto separado; ver [diagnóstico dos downloads](../network-qos/DIAGNOSTICO-DOWNLOADS-ANTES-DEPOIS-20261014.md).

| Plataforma | ID R149 a conferir no recebimento |
| --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-29c3d7b9e6a8` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-dcad075758e5` |
| GameCube | `dolphin-5102a033-gamecube-v3-c80ec04d1723` |
| Wii | `dolphin-5102a033-wii-v3-c80ec04d1723` |
