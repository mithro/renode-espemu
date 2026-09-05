idf_component_register(SRCS "test_spi2.c"
                       INCLUDE_DIRS "."
                       REQUIRES driver esp_driver_spi esp_driver_gpio)
