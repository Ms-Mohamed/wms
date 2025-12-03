import { memo, useMemo } from 'react';
import { Box, VStack, Link, Text, useColorModeValue, Icon } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useLocation, Link as RouterLink } from 'react-router-dom';
import {
  HomeIcon,
  ShoppingCartIcon,
  CubeIcon,
  ChartBarIcon,
  BeakerIcon,
} from '@heroicons/react/24/outline';

interface NavItem {
  path: string;
  label: string;
  icon: any;
}

const Sidebar = memo(() => {
  const { t } = useTranslation();
  const location = useLocation();
  const bgColor = useColorModeValue('white', 'gray.800');
  const borderColor = useColorModeValue('gray.200', 'gray.700');
  const activeBg = useColorModeValue('blue.50', 'blue.900');
  const activeColor = useColorModeValue('blue.600', 'blue.300');

  const navItems: NavItem[] = useMemo(
    () => [
      { path: '/dashboard', label: t('nav.dashboard'), icon: HomeIcon },
      { path: '/orders', label: t('nav.orders'), icon: ShoppingCartIcon },
      { path: '/products', label: t('nav.products'), icon: CubeIcon },
      { path: '/stock', label: t('nav.stock'), icon: BeakerIcon },
      { path: '/analytics', label: t('nav.analytics'), icon: ChartBarIcon },
    ],
    [t]
  );

  return (
    <Box
      w="250px"
      h="100vh"
      bg={bgColor}
      borderRight="1px"
      borderColor={borderColor}
      p={4}
      position="sticky"
      top={0}
    >
      <VStack align="stretch" spacing={2}>
        <Text fontSize="xl" fontWeight="bold" mb={4} color="blue.600">
          WMS
        </Text>
        {navItems.map((item) => {
          const isActive = location.pathname === item.path;
          return (
            <Link
              key={item.path}
              as={RouterLink}
              to={item.path}
              display="flex"
              alignItems="center"
              px={4}
              py={3}
              borderRadius="md"
              bg={isActive ? activeBg : 'transparent'}
              color={isActive ? activeColor : 'gray.700'}
              fontWeight={isActive ? 'semibold' : 'normal'}
              _hover={{
                bg: isActive ? activeBg : 'gray.100',
              }}
            >
              <Box as={item.icon} w={5} h={5} mr={3} />
              {item.label}
            </Link>
          );
        })}
      </VStack>
    </Box>
  );
});

Sidebar.displayName = 'Sidebar';

export default Sidebar;
