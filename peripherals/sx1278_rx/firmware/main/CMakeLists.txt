idf_component_register(SRCS "test_sx1278_rx.c"
                       INCLUDE_DIRS "."
                       REQUIRES driver esp_driver_spi esp_driver_gpio)
