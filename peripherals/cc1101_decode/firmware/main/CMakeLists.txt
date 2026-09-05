# The test firmware COMPILES the actual product FSK decoders
# (decode_fineoffset.c + decode_common.c). These are vendored under decoders/
# from the firmware repo esp32-to-433mhz (firmware/decoders/) so this suite
# exercises the SHIPPED decoder against injected real weather/moisture frames,
# not a reimplemented proxy. See decoders/VENDORED.md for provenance and the
# sync note (they are copied, not symlinked, because the Renode CI job checks
# out only this repo).
idf_component_register(SRCS "test_cc1101_decode.c"
                            "decoders/decode_fineoffset.c"
                            "decoders/decode_common.c"
                       INCLUDE_DIRS "." "decoders"
                       REQUIRES driver esp_driver_spi esp_driver_gpio)
