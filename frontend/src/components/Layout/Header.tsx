import { memo, useCallback } from 'react';
import { Avatar, Flex, Menu, MenuButton, MenuDivider, MenuItem, MenuList, Select, Text } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { useNavigate } from 'react-router-dom';

const Header = memo(() => {
  const { i18n, t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const onLang = useCallback((e: React.ChangeEvent<HTMLSelectElement>) => {
    i18n.changeLanguage(e.target.value);
    localStorage.setItem('language', e.target.value);
  }, [i18n]);

  const name = user?.sub ?? '';

  return (
    <Flex as="header" h="56px" px={6} align="center" justify="flex-end" gap={3} bg="white" borderBottom="1px solid" borderColor="ink.100" flexShrink={0}>
      <Select size="sm" w="auto" variant="filled" value={i18n.resolvedLanguage ?? i18n.language} onChange={onLang} aria-label="Language">
        <option value="fr">FR</option>
        <option value="en">EN</option>
      </Select>
      <Menu placement="bottom-end">
        <MenuButton aria-label="Account">
          <Flex align="center" gap={2}>
            <Avatar size="sm" name={name} bg="brand.100" color="brand.700" />
            <Text display={{ base: 'none', sm: 'block' }} fontSize="sm" fontWeight={600}>{name}</Text>
          </Flex>
        </MenuButton>
        <MenuList boxShadow="pop" borderRadius="lg">
          <Text px={3} py={1} fontSize="xs" color="ink.500">{user?.role}</Text>
          <MenuDivider />
          <MenuItem onClick={() => { logout(); navigate('/login'); }}>{t('auth.logout')}</MenuItem>
        </MenuList>
      </Menu>
    </Flex>
  );
});

Header.displayName = 'Header';
export default Header;
