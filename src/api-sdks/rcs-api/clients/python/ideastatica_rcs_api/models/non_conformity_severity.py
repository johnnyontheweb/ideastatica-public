# coding: utf-8

"""
    RCS Rest API 1.0

    IDEA StatiCa RCS API, used for the automated design and calculation of reinforced concrete sections.

    The version of the OpenAPI document: 1.0
    Contact: info@ideastatica.com
    Generated by OpenAPI Generator (https://openapi-generator.tech)

    Do not edit the class manually.
"""  # noqa: E501


from __future__ import annotations
import json
from enum import Enum
from typing_extensions import Self


class NonConformitySeverity(str, Enum):
    """
    Nonconformity severities
    """

    """
    allowed enum values
    """
    INFO = 'info'
    WARNING = 'warning'
    ERROR = 'error'
    TERMINATEDERROR = 'terminatedError'

    @classmethod
    def from_json(cls, json_str: str) -> Self:
        """Create an instance of NonConformitySeverity from a JSON string"""
        return cls(json.loads(json_str))


