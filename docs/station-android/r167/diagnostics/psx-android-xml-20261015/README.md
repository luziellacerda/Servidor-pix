# PSX: correção Android da preparação XML — 15/10/2026

A R167 instalada no moto g52 foi identificada pelo SHA-256 lido no próprio
telefone. Ao iniciar Capcom vs. SNK / VS, a sala aceitou os dois jogadores e
o comando de início; o APK falhou em `DocumentBuilderFactoryImpl.setFeature`
com `ParserConfigurationException`, antes de conectar o motor. A falha local
foi reproduzida executando o método original no framework Android.

A correção está no fonte canônico `integration/psx-online/java/org/emulationstation/frontend/netplay/StationPsxLaunch.java`.
Troca somente `copyPresentationSettings` e seus imports: usa SAX do Android,
recusa DTD/entidades externas e mantém leitura limitada e cancelável. Aceita
o formato real do EmulationStation, que tem entradas XML irmãs, inclusive
`map`; a cópia privada preserva esse formato. Copia apenas os dois nomes de
controle e `LibretroStretch`, sem gravar no arquivo normal. O leitor do
`libmain.so` exato foi conferido por análise estática.

## Resultado e limite da prova

- Método original: falha Android reproduzida com a exceção capturada.
- Método corrigido: **29 verificações no Motorola**, incluindo as preferências
  reais, cancelamento, DTD, entidade externa e valores inválidos.
- Session/Launch completo: **64 verificações JVM**, com fixtures Android/JNI
  explícitas; não são execução nativa nem gameplay físico.
- O teste USB envia só um DEX de diagnóstico para diretório temporário próprio,
  executa o método extraído e remove esse diretório. Não instala ou abre o app.
- **Nenhum APK novo foi compilado, assinado ou instalado neste Linux.** O APK
  R167 instalado continua com o defeito. Reinstalá-lo não incorpora a correção.

## Compilação de produção

Usar `build-contract.json`: doador assinado R167 exato, fonte congelado e
assinatura original. A classe anônima SAX acrescenta `StationPsxLaunch$1`;
`classes35.dex` passa de 608 para 609 classes, preservando as outras 607. A receita
R166 que exige uma única classe não pode ser reutilizada sem adaptação.
Preservar as outras 39 DEX, o carrossel R167, núcleo, dependências, opções e
controles. Atualizar de forma coerente apenas o vínculo do payload e seus
marcadores existentes. Entregar os hashes finais e o kit independente exato.
O servidor deve conferir esse novo kit sobre baseline fresca; não repetir
o cadastro R167 já aplicado, nem inventar identidades de motor.

Publicar pelo instalador USB permanente, preservando dados e a escolha manual
do usuário. O PC de produção está offline no conector; esta entrega fornece
o código corrigido e a prova reproduzível, sem alegar um APK novo disponível.
Depois da instalação manual, repetir o piloto Capcom VS/2P e registrar cada
hash instalado, vídeo, áudio, controles e saída do anfitrião.

## Reproduzir a prova no Linux com um telefone autorizado

Use um diretório de saída novo e privado. O runner exige exatamente um USB
autorizado e ferramentas SDK locais; os caminhos abaixo correspondem a este PC.

```bash
python3 integration/psx-android-xml-20261015/tests/run_android_probe.py \
  --source integration/psx-online/java/org/emulationstation/frontend/netplay/StationPsxLaunch.java \
  --adb /home/lz-servidor/android-sdk/platform-tools/adb \
  --android-jar /home/lz-servidor/android-sdk/platforms/android-36/android.jar \
  --d8 /home/lz-servidor/android-sdk/build-tools/36.0.0/d8 \
  --javac /usr/bin/javac --out /caminho/privado/novo-resultado
python3 integration/psx-online/tests/run_session_probe.py \
  --jdk /usr --out /caminho/privado/novo-resultado-jvm
```

Acrescentar `--baseline` somente com o fonte original se for necessário
reproduzir a recusa Android antiga. Não é necessário repetir esta prova
para cadastrar os mesmos motores ou limpar dados do telefone.

O incidente e os recibos sanitizados estão no mesmo handoff principal e em
`r167/diagnostics/psx-android-xml-20261015/`. Logs completos, serial,
preferências e detalhes de salas ficam no diretório privado do operador.

Referências primárias: [DOM Android](https://developer.android.com/reference/javax/xml/parsers/DocumentBuilderFactory),
[DefaultHandler2 Android](https://developer.android.com/reference/org/xml/sax/ext/DefaultHandler2),
[Settings do EmulationStation](https://github.com/Aloshi/EmulationStation/blob/master/es-core/src/Settings.cpp).
A compatibilidade foi comprovada no framework e no binário exatos recebidos;
essas referências explicam os contratos e não substituem as provas locais.
