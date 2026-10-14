# R148 — sinopses dos sistemas removidas da apresentação

A R148 é uma **candidata assinada**, baseada no APK R147 SHA-256
`c038a0d75113359187d8eb9d2762e8d7e4ceee1f3538d1982313fc1e3a781ad6`.
A R143 permanece como referência estável. A fonte R148 está congelada no commit
`aae9068ca55fd5883606156fe839edec06b816c6`.

O ramo `systemsMode` do carrossel deixa o corpo da descrição vazio. O título do
sistema, seleção e botão continuam. Os ramos de sinopses de **jogos** e de
**coleções** permanecem nos mesmos bytes de fonte; as descrições do servidor
nos detalhes dos jogos também permanecem. O overlay troca uma linha no
cabeçalho nativo e inverte integralmente para o cabeçalho original. Antes da
mudança, o carrossel R147 foi reconstruído com os 158 inputs originais e
reproduziu exatamente o SHA-256
`2e15a9a330c2562c71574921f5558d6142e3051bba64a315434e5714f38908a9`.
O carrossel novo tem SHA-256
`313de078f8c300b6c7944de2ce03292de697532bc1a493b7406fdbb8260fbd28`;
exports, dependências, SONAME e alinhamento nativo foram conferidos.

O carrossel é um dos 345 payloads vinculados aos três marcadores dos motores
independentes. Por isso, os três marcadores e os quatro IDs correspondentes no
`engines.json` foram renovados junto com ele. O APK muda **cinco entradas**:
carrossel, marcadores PSX/N64/Dolphin e registro de motores. As outras 13.855
entradas são iguais às da R147, incluindo os **40 DEX**, o core Dolphin e a
rota direta v3 da R146. Nenhuma lógica de download, sala, controle, perfil,
opções, CPU ou JIT foi alterada nesta revisão.

APK assinado R148: SHA-256
`c8b8c30cac7d36ec2a8ca8d470542096919435c749dede535cd0d58648e00dc1`,
2.715.089.935 bytes. Assinatura com o certificado original, esquemas v2/v3 e
alinhamento 16 KiB passaram. As 13.860 entradas do APK assinado foram lidas e
comparadas com a R147.

O kit privado correspondente à R148 tem SHA-256
`e94e2dcc7436c310da6fac6a8d0ab366f496e788146fa3d8ce28e062e244ce19`,
12.180.697 bytes e 38 arquivos. Contém seis ELF, vinte dependências e os
mesmos 78 modos revisados: 66 N64, nove GameCube e três Wii; zero novas
aprovações PS1 HLE. O kit R147 é de revisão anterior e não cadastra estes
quatro IDs novos.

**Instalação:** a R148 foi instalada por atualização (`adb install -r`) no
Samsung A56 em 14/10/2026 às 12:09 UTC. O instalador retornou `SUCCESS` e
conferiu no aparelho o SHA-256 integral do APK
`c8b8c30cac7d36ec2a8ca8d470542096919435c749dede535cd0d58648e00dc1`.
Não houve desinstalação nem limpeza de dados; a primeira instalação permaneceu
inalterada na comparação antes/depois. O instalador não abriu o aplicativo.
O [recibo sanitizado](evidence/installation-a56-sanitized.json) registra o
escopo da conferência, sem identificação USB ou código de licença.

**Pendências:** não há recibo de aplicação do kit no servidor, teste visual no
telefone ou partida física R148. O servidor deve conferir o APK e kit
correspondentes, obter baseline protegido fresco e registrar aditivamente os
quatro IDs/modos pelo procedimento vigente sob `scan.lock`. Não promover esta
candidata ao consumidor antes da validação. Consultar `STATUS.json` para os
hashes, entradas alteradas e estados exatos.
