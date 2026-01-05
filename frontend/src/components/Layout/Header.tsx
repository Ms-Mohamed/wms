import { memo, useCallback } from 'react';
import { Box, Flex, Select, Text } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { GlobeAltIcon } from '@heroicons/react/24/outline';

const Header = memo(() => {
  const { i18n } = useTranslation();

  const handleLanguageChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      const lang = e.target.value;
      i18n.changeLanguage(lang);
      localStorage.setItem('language', lang);
    },
    [i18n]
  );

  return (
    <Box
      bg="white"
      borderBottom="1px"
      borderColor="gray.200"
      px={6}
      py={4}
      shadow="sm"
    >
      <Flex justify="space-between" align="center">
        <Text fontSize="2xl" fontWeight="bold" color="gray.800">
          Warehouse Management System
        </Text>
        <Flex align="center" gap={4}>
          <Flex align="center" gap={2}>
            <GlobeAltIcon style={{ width: 20, height: 20 }} />
            <Select
              value={i18n.language}
              onChange={handleLanguageChange}
              size="sm"
              w="120px"
            >
              <option value="fr">Français</option>
              <option value="en">English</option>
            </Select>
          </Flex>
        </Flex>
      </Flex>
    </Box>
  );
});

Header.displayName = 'Header';

export default Header;

