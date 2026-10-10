# Copyright 2024 Bradley D. Nelson
# Modifications Copyright 2026 Luginfo10
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

cmake_minimum_required(VERSION 3.13)

# Default to a plain Raspberry Pi Pico (override with -DPICO_BOARD=...).
if (NOT DEFINED PICO_BOARD)
  set(PICO_BOARD pico)
endif()

# import the pico-sdk (shared with the pico-ice port)
set(PICO_SDK_PATH ${CMAKE_CURRENT_SOURCE_DIR}/../pico-ice/pico-sdk/)
include(${CMAKE_CURRENT_SOURCE_DIR}/../pico-ice/pico_sdk_import.cmake)

# configure the pico-sdk project
project(lugforth_pico C CXX ASM)
pico_sdk_init()

add_executable(${CMAKE_PROJECT_NAME} main.c)
target_link_libraries(${CMAKE_PROJECT_NAME}
    pico_stdlib
    pico_stdio_usb
    hardware_adc
    hardware_gpio
)
target_include_directories(${CMAKE_PROJECT_NAME} PRIVATE
   ${CMAKE_CURRENT_SOURCE_DIR}/../
   ${CMAKE_CURRENT_SOURCE_DIR}/../out/
)
target_include_directories(${CMAKE_PROJECT_NAME} PUBLIC
    ${CMAKE_CURRENT_LIST_DIR}
    ${CMAKE_CURRENT_BINARY_DIR}
)
pico_add_extra_outputs(${CMAKE_PROJECT_NAME})
# The console is the USB CDC serial port provided by the pico-sdk.
pico_enable_stdio_usb(${CMAKE_PROJECT_NAME} 1)
pico_enable_stdio_uart(${CMAKE_PROJECT_NAME} 0)
